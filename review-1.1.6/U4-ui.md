# U4-ui: release check of 1.1.6 (999a100)

Slice: src/Ui/FullView.Visuals5.cs, Visuals6, Weather, Week, Wifi, Zoom, FullView.cs, GameEditor, Glyphs, Kit, Menus, Motion, Paper, SpeedWidget, StreamOverlay, SupportCard, TermView, Theme, Touch, Typing, WeatherWidget (6,041 lines, all read). I also traced Controls.cs (DB AccessibleObject, LayeredForm, switches), AppCtx.OnUi/Render, Weather.cs (reading side) and Monitor.Snap as callers.
Harness: .../scratchpad/review5/U4-ui/ (v.cs: VNum/RowMs and the fr table through reflection on r6.dll; sc.cs: SupportCard size under xvfb; cmap2.py: the Barlow cmap check)

No crashes, security issues or rule breaks turned up in this slice. The real finding is #1: decimal commas are misread, and it affects anyone whose Windows uses a comma decimal (fr-CA, es, de...).

### 1. Tool pictures misread decimal-comma numbers: "99,8% smooth" is drawn as an 8% bar, "12,3 GB" as 3 GB
Severity: wrong result  
Where: src\Ui\FullView.Visuals.cs line 166 (VNum), used by src\Ui\FullView.Visuals6.cs lines 53 (sixmonths `([0-9.]+)%`), 54 (wakeup `([0-9.]+)\s*s on average`), 57-59 (gpu, bg, uptime), 77-78 (pressure "% full", "°C"), 254 (Forecast `([0-9.]+)\s*GB`), and src\Ui\FullView.Visuals3.cs line 20 (RowMs `([0-9]+(?:\.[0-9]+)?)\s*ms`). The producers format with CurrentCulture: src\Checks\OverTime.cs lines 148, 247 and 262 (N1 = `ToString("0.#", CurrentCulture)`), src\WindowsDeep\HwTools.cs line 263, PcHealth.cs line 197.  
How to reproduce: Windows region French (Canada) or Spanish or German. Toolbox > Six months (or Data cap forecast, Wake-up time, Memory/disk pressure). Ran in the harness: with the fr-CA culture, VNum("99,8% smooth", `([0-9.]+)%`) = 8, VNum("12,3 GB down + up in 24 days", `([0-9.]+)\s*GB`) = 3, VNum("4,3 s on average", ...) = 3, RowMs("12,5 ms") = 5.  
What happens / what should happen: the regexes stop at the comma, so they read only the decimals. The six-months bars show 8 % for a 99.8 % smooth month (the label beside the bar says "8 %"). The forecast draws "today: 3 GB" with the pace line built from 3 GB. Wake-up bars and the memory and disk gauges read the same way. VNum's `.Replace(",", "")` never gets a chance, because `[0-9.]+` doesn't capture the comma. Either the producers should write these values with InvariantCulture, or VNum and RowMs should accept `[0-9.,]` and parse with the culture that wrote them. The table rows are right; only the pictures lie.  
Confidence: confirmed (ran VNum and RowMs through reflection on r6.dll under fr-CA; producer formats read)

### 2. [day 1] The stream lag graph shows a red "no reply" before anything has been measured
Severity: wrong result  
Where: src\Ui\StreamOverlay.cs lines 89-97 (Paint1: `now = v.Length > 0 ? v[^1] : NaN`, then NaN means red "no reply"), line 77 (Series: `M.NetPing.Values(...)`, empty at first)  
How to reproduce: right after first start, or for a game that has just been added and has no samples yet, open Streaming > lag graph. Ring.Values returns an empty array until the first sample.  
What happens / what should happen: on an empty series the big number reads "no reply" in red, which tells viewers the line is down. It should say "waiting" in grey until there is a sample. The window also isn't translated: "Internet ping", "no reply", "spikes", "jitter", "loss" and the four menu items.  
Confidence: suspected (read)

### 3. The corner widget card stays in English in fr/es/ar/bn: the level pill, and all of the Calls, Engineer and Gamer cards
Severity: cosmetic  
Where: src\Ui\Theme.cs line 864 (`Theme.Pill(g, Word(s.Level), ...)`, Word at line 439 has no L.T); lines 777-781 (CallsCard: "LINE OK / SOME BREAKUP / CHOPPY / WAITING", every headline, "ms jitter", "delay … loss …", "through the VPN"); lines 786-789 (EngineerCard: "OK/SLOW/PROBLEM", "ms internet", "router … DNS … jitter … loss"); lines 728-765 (GamerCard: "Waiting", "Competitive/Casual/Laggy", "Spike … ms:", " is steady.", "jitter … loss", "region estimate / Valve server / exact"); line 902 (the call box: "jitter", "loss")  
How to reproduce: Settings > Language > Français, restart, then hover the corner widget (and set its style to Calls or Engineer, or start a game). Harness: in fr, L.T("Problem") = "Problème" and L.T("Busy") = "Occupé", but Draw.Word(2) = "Problem". "LINE OK", "CHOPPY", "Competitive" and "Laggy" have no table entries at all.  
What happens / what should happen: the most visible surface in the app mixes English into a translated card. The pill should go through L.T, and these strings should be added to the tables.  
Confidence: confirmed (ran L.T and Draw.Word through reflection)

### 4. Widget card: the "Why:" line is measured in English and drawn translated, so its last line can be cut off
Severity: cosmetic  
Where: src\Ui\Theme.cs line 846 (`Measure("Why: " + s.Hint, ...)`) against line 871 (`DrawString(L.T("Why: ") + s.Hint, …, new RectangleF(…, hintH))`)  
How to reproduce: use French ("Pourquoi : " is 6 characters longer than "Why: ") and get a hint whose English form just fits on one line, for example a level-1 hint.  
What happens / what should happen: the translated text wraps to one more line than the rectangle was sized for, and GDI+ clips the extra line. Measure the same string that gets drawn.  
Confidence: confirmed (read; fr lookup ran); the clipping itself not seen on screen

### 5. The support (heart) card is taller than the screen at 150% scaling and can't scroll; its last row is unreachable
Severity: cosmetic  
Where: src\Ui\SupportCard.cs line 31 (`ClientSize = (640*S, Measure())`, `Draw.FitOnShow(this, false)`, no shrink, no scroll); src\Ui\FitScreen.cs line 21 (only moves it)  
How to reproduce: a 1920x1080 laptop at 150% (Windows' default for it, about 1,010 px of working area). Choose Bengali or Arabic and open the heart at the bottom of the sidebar. Measured with mono: bn is 960x1058 and ar is 960x1084 at S=1.5. English failed to measure on mono (private-font limitation) but will be near the same height.  
What happens / what should happen: FitScreen pins the card's top to the top of the work area and the bottom 50-80 px are off screen, including the thank-you line and part of the "Or help for free" buttons ("Share an idea", "Report a bug"). The card should be capped to the work area with its content scrolling, or laid out in two columns when it is short of room.  
Confidence: confirmed (sizes measured under mono and xvfb; Windows text metrics differ a little)

### 6. AdeeboShell's caret blink repaints the input row twice a second even when nothing changes on screen
Severity: slow  
Where: src\Ui\TermView.cs line 42 (530 ms blink timer: `caretOn = !caretOn …; InvalidateCaret()`), lines 183-186 (unfocused, the caret is drawn as the same outline whatever caretOn is), lines 206-212 (InvalidateCaret runs Relayout(), which re-wraps the last line, on every blink)  
How to reproduce: open AdeeboShell, then click into another app so the window stays visible but loses focus, or Tab to another control on the page. Count paints on the TermView.  
What happens / what should happen: about two repaints a second plus a re-wrap of the input line, all producing the same pixels. Rule 7 says no repaints when nothing changed. When `!Focused` (or the form is inactive), the blink tick should skip the Invalidate, or stop the timer until focus returns.  
Confidence: suspected (read)  
Rule: 7

### 7. Zooming by pinch, Ctrl+wheel or a held Ctrl+= rebuilds the whole window once per queued step
Severity: slow  
Where: src\Ui\FullView.Zoom.cs lines 28-34 (ZoomTo: `Cfg.Zoom = z; Cfg.Save(); BeginInvoke(ReopenAsked)`), src\Ui\Touch.cs lines 158-165 (a pinch calls TouchZoom on every quarter of spread), src\AppCtx.cs lines 410-415 (ReopenAsked: `old.Close(); OpenFull()`)  
How to reproduce: on a touchscreen, spread two fingers wide in one motion. Or turn the wheel three notches quickly with Ctrl held, or hold Ctrl and = so it auto-repeats.  
What happens / what should happen: each step updates Cfg.Zoom, saves the settings file and queues another ReopenAsked before the first one has run. The window then closes and is rebuilt two or three times in a row (every page, every control), with a visible flash each time. Coalesce the steps: one pending reopen, which picks up the latest Cfg.Zoom when it runs.  
Confidence: suspected (read)

### 8. Arabic: the tray menus and the widget card are drawn left-to-right
Severity: cosmetic  
Where: src\Ui\Menus.cs lines 85-113 (OnRenderItemText draws at TextRectangle.X with no DirectionRightToLeft or right alignment; OnRenderArrow always draws a right-pointing chevron), although src\AppCtx.cs line 74 sets `menu.RightToLeft = Yes`; src\Ui\Theme.cs lines 862-871, 887-889 and 916-917 (the widget card's name, headline, hint, row labels and totals are drawn with no RTL StringFormat)  
How to reproduce: switch the language to العربية, restart, then right-click the tray icon, open a submenu, and hover the widget.  
What happens / what should happen: in the mirrored menu, Arabic text hugs the far (left) edge instead of the icon margin, and the submenu chevron points away from its submenu. On the card, wrapped Arabic headlines get an LTR paragraph direction, so their final punctuation lands on the wrong side. Kit.Tile and Kit.Header already handle RTL, and these two surfaces should do the same.  
Confidence: suspected (read)

### 9. "Your week" on Home: in French the activity names run into the level dot and the line after it
Severity: cosmetic  
Where: src\Ui\FullView.Week.cs lines 32-34 (title at x+44·S, dot at x+150·S, line at x+166·S: a fixed 106 px column)  
How to reproduce: use French and have a week with streaming or trading recorded. The titles are "Diffusion en continu" and "Négociation" (lang-fr.txt). "Diffusion en continu" in Barlow Bold at 13 px is about 130 px wide.  
What happens / what should happen: the title is drawn over the dot and the start of the summary line. The column should be measured from the widest translated title, or the title cut with an ellipsis.  
Confidence: suspected (read; string widths estimated)

### 10. Wi-Fi walk test: the Rename and Remove links are sized and placed from the English words
Severity: cosmetic  
Where: src\Ui\FullView.Wifi.cs lines 157-161 (`rn = MeasureString("Rename")`, `dr.X = ax + rn.Width + 14*S`, `MeasureString("Remove")`, while L.T("Rename") and L.T("Remove") are what get drawn)  
How to reproduce: use French or Bengali and record two rooms in the walk test.  
What happens / what should happen: "Renommer" is wider than "Rename", so it runs into "Supprimer", and both click targets stay English-sized: clicking the end of a translated word does nothing. Measure the translated strings. The status lines in the same file are also only half translated (" to ", "... stay still for 10 seconds.", ". Walk to the next room.", "Start walk test →", "Best: … Weakest: …").  
Confidence: confirmed (read)

### 11. Tool picture captions go back to English whenever "+N more" is added
Severity: cosmetic  
Where: src\Ui\FullView.Visuals5.cs lines 330, 379, 399 and 459; src\Ui\FullView.Visuals6.cs lines 186-187, 208, 229 and 388 (`cap += "  ·  +" + n + " " + L.T("more")`, after which VisCard calls `L.T(caption)` on the combined string, line 178 of Visuals.cs)  
How to reproduce: use French and run a tool whose result has more rows than its picture shows, for example Open ports with more than 18 doors, or MTR with more than 10 hops.  
What happens / what should happen: the combined string isn't in the table, so the caption appears in English only when the result is big. Translate the caption before appending the count.  
Confidence: suspected (read)

### 12. Sniff leaks a process handle each time it runs
Severity: slow  
Where: src\Ui\FullView.cs line 1318 (`var p = Process.Start(psi); Job.Add(p);`; the Process is never disposed, and the Exited handler doesn't dispose it either)  
How to reproduce: Connections > Sniff on a row, about 20 times in one session, and watch the handle count in Task Manager.  
What happens / what should happen: each sniff keeps a process handle and a wait handle until the GC finalizes them. The Exited handler should call `p.Dispose()` after reading the result. It is small, but the deep test checks handles.  
Confidence: suspected (read)

### 13. Game editor: an imported game name skips the 40-character limit, and the dialog's choices aren't translated
Severity: cosmetic  
Where: src\Ui\GameEditor.cs line 190 (`name.Box.Text = f[2].Trim()`; MaxLength only limits typing) against line 106/109 (the uniqueness check uses the full name) and Gamer.cs line 129 (it saves `Clean(g.Name, 40)`); lines 23, 29, 88-109 and 140 ("Auto (ping, then TCP)", "Add a game", "Edit ", "Line n: …", "no reply" have no L.T)  
How to reproduce: Import a list whose game line has a 60-character name. Then import another whose name has the same first 40 characters.  
What happens / what should happen: both pass the "already have a game called" check, then both are saved under the same 40-character name. The name should be clipped to 40 on import, before the check. Translate the combo items and the messages.  
Confidence: suspected (read)

## Still present (from round 4, in this slice)

| Round-4 # | Status at 999a100 |
|---|---|
| 70 (minimise stops a running tool) | fixed: FullView.cs line 396 keeps the window minimised while tbBusy or sniffing |
| 111 (idle repaints on What changed, Story, running tool) | fixed: keyed (changesKey, travelKey, tbBusyKey with HopsPrint) |
| 112 (new font on every widget draw) | fixed: fCardNum and fZ are made once |
| 119 (high contrast: BlueSoft, live switch) | fixed: Apply maps BlueSoft; ContrastChanged and AppliedSig re-apply |
| 131 (GameEditor "8 are watched at once") | fixed: uses GamePing.MaxServers |
| 134 (keyboard press earns no "page used") | fixed: WireKeys.Press calls PageUsed |

## Checked and held up

- **Fonts.** Parsed the cmap of all four embedded Barlow faces: every symbol the app draws is present, including ↓ ↑ → ← › … · ★ ▲ ▼ ● ✕ ° × − – — ✓ ⋯ ≈ ±. Only ₹ ৳ ⚠ are missing, and the code uses none of them. ar uses Segoe UI and bn uses Nirmala UI, in Ui(), UseLanguageFonts, Serif and Paper.Fonts. zh is not offered (Lang.cs line 16). A zoom change never disposes a font that is still held: the sets are kept, and UseLanguageFonts clears the cache without disposing. Paper.Fonts disposes only its own fonts.
- **Corner and weather widgets.** LayeredForm is click-through (TRANSPARENT + LAYERED + NOACTIVATE + TOOLWINDOW) and excluded from capture (WDA_EXCLUDEFROMCAPTURE). It hides while presenting or in full screen (SHQueryUserNotificationState 2/3/4) and while the view is open on the same screen. TOPMOST is re-claimed once every 30 s, not on each draw. The folded chip redraws only when its key changes: speeds every 2 s at most, the rest every 5 s at most. The weather chip and card show nothing until a forecast hour less than 1.5 h old exists, so day 1 draws no weather. The per-tick cost of WeatherKeyPart is two scans of at most about 120 hours under the lock, and Current parses one short string.
- **Day-1 widget.** The initial Snap has Path all 3 (grey), NaN rates show as "-" through Fmt and Rate, and the gamer card says "Waiting" when there are no samples.
- **Touch.cs.** GESTUREINFO's sequential layout matches the native struct on x86 and x64. Finger() masks 0xFFFFFF00, so the sign-extended LPARAM on x64 is fine. ListView, TextBox, ComboBox, TreeView and WebBrowser keep their own panning. A gesture answered here is closed here, and one passed on is left to DefWindowProc.
- **Controls.cs accessibility.** DB.DbAcc/Item checks the index on every read (a list that shrinks returns an empty name and Rectangle.Empty, never an exception). DoDefaultAction goes through BeginInvoke, and SwitchAccess reports the Checked state.
- **BeginInvoke after close.** Guarded with an IsDisposed check plus catch in: the watchdog, RefreshConns, StartDest, StartHealth, ToggleLive, the Sniff callbacks, PickPlace, GameEditor.TestAll, the Weekly callback and PickRunning. The FullView watchdog Timer is disposed in FormClosed.
- **Timers.** Typing's 33 ms timer stops when nothing is typing. Motion reads SPI_GETCLIENTAREAANIMATION at most every 10 s and is off in Penny mode. The StreamOverlay tick repaints and writes the PNG only when its key changes, and the PNG is not rewritten if its bytes are identical.
- **Visuals5/6 crash paths.** Empty lists fall back to example shapes. `(int)VNum` on NaN ends up with the defaults. `First()` after `Sum > 0` is safe. Bars and HopRoad take Max only on lists that are not empty. Leaks' `double.Parse` sees only "0"/"0.0" values from Gb(), so it can't throw. DateTime and hex parses use TryParse.
- **GDI.** Pens and brushes are made in `using`. MakeIcon destroys its HICON, Glyphs caches its paths and catches parse errors, and the Kit.Sent and Draw.Measure caches are bounded. StreamOverlay disposes its fonts and menu. Menus.Round replaces the Region, and the old one is disposed.
