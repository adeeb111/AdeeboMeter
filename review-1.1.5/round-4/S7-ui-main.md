# S7: main UI (FullView, Toolbox, ToolHome, Tools, Examples, Settings, Overview, Paint, Controls, Theme, examples.txt, lang-fr.txt)

Code: /home/user/AdeeboMeter-dev at e80412e. The harnesses (reflection on build4/r4.dll, run under mono) are in scratchpad/review4/s7/: m.cs (Toolbox search), sm.cs/sm2.cs (Settings search), rt.cs (settings round trip), lab.cs (policy vs iperf), url.cs (URL rewrite), rc.cs (Recolor), seg.cs (Settings row widths), ex.py (example registry vs tool registry), fr.py/fr2.py (French table).

### 1. traffic_lab = off doesn't stop the Traffic Lab's own iperf3, bufferbloat, DSCP ping or Wake-on-LAN
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 242 (LabExempt = wol, iperf, bloat, dscp), line 728 (the gate is only applied to LabGate / SenderGate), lines 50-53 and 140 (Do lambdas call Toolkit.Iperf / Loaded / DscpPing / Wake directly); src\Checks\Toolkit.cs line 158 (Iperf: no Policy.NoLab check)  
How to reproduce: AdeeboMeter.policy with `traffic_lab = off`. Toolbox › Traffic Lab › LOAD & QUEUES › iperf3 to your server, type a server, Run. The same goes for Latency under load, DSCP-marked ping and Wake-on-LAN, which sit in the same Traffic Lab section.  
What happens / what should happen: the test runs. Under the harness, with Policy.NoLab = true, Toolkit.Iperf("127.0.0.1:15201", "-t 1") still opened a connection to the local test server (1 connection accepted; the result rows were "Settings TCP · 1 stream · 1 s", then "Upload failed: server closed"). The policy file says `traffic_lab = off` covers "Toolbox › Traffic Lab (building and sending test packets)", and iperf3 can send UDP at whatever `-b` the user types. LabExempt only keeps these tools out of the "my own network" tick. It shouldn't take them out of the IT switch: call Lab.Allowed's NoLab check (or Policy.NoLab directly) for every tool whose section is "lab".  
Confidence: confirmed (ran it: harness lab.cs, Policy.NoLab=true, local TCP listener counted the connection)  
Rule: 6 (adjacent: the policy fails to switch off what it names)

### 2. speed_test = off doesn't stop "Find the closest server for me", which also skips the private-address guard
Severity: security  
Where: src\Ui\FullView.Overview.cs lines 53-80 (FindClosestSpeed), line 39 (menu item), line 28 (SpeedMenu: no policy check)  
How to reproduce: AdeeboMeter.policy with `speed_test = off`. Overview › the "Server: … · change" chip › Find the closest server for me.  
What happens / what should happen: a DNS lookup, then one ping (or a TCP connection to port 443) goes to every speed-test server: Cloudflare, every Vultr and OVHcloud city, and every entry in the user's speedservers.txt. SpeedTest.Measure and SpeedTest.Run both refuse under the policy (SpeedTest.cs lines 115 and 173). This path never checks it. It also skips Measure's `Reachable(ip)` guard, so a speedservers.txt name that resolves into the LAN or loopback gets probed. It should check Policy.NoSpeed, hide or grey the server menu under the policy, and apply the same Reachable filter.  
Confidence: suspected (read)  
Rule: 6 (adjacent)

### 3. Certificate chain compare loses the port of a pasted URL and checks the wrong server
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 275 (UrlTools has no "certcmp"), lines 716-723 (StartTool URL rewrite; the label "Site - e.g. outlook.office.com" has no ":port", so withPort = false)  
How to reproduce: Toolbox › Certificate chain compare, Site = `https://intranet.example.com:8443/login`, Run.  
What happens / what should happen: StartTool rewrites the box to `intranet.example.com` before EngPack.CertCompare sees it. The harness url.cs printed `https://intranet.example.com:8443/login -> intranet.example.com`. The compare then handshakes with port 443, a different service or a refused port, and stores that snapshot under the run name. CertCompare parses URLs itself and keeps the port (EngPack.cs line 44), and its own error text even suggests "https://intranet.example.com". Fix: add certcmp to UrlTools, or pass withPort when the URL has an explicit port.  
Confidence: confirmed (ran it: url.cs over Tools2() with the same skip rules as StartTool)

### 4. Export settings → Import settings silently drops six settings
Severity: wrong result  
Where: src\Program.cs line 370 (Cfg.Known) against lines 359-366 (Cfg.Lines); src\Ui\FullView.Settings.cs line 544 (ImportSettings keeps only Known keys)  
How to reproduce: Settings › Voice = Brutal, Certificate revocation = On, Buddy = Off, then Export settings… and Import that file on another PC (or the same PC after changing those back).  
What happens / what should happen: "Loaded N settings", but the voice, revocation, buddy, hat, outagebest and overlay position are unchanged. Export copies the real settings file, which contains `mood=`, `crlrev=`, `buddy=`, `hat=`, `outagebest=` and `ovat=`, but Known lacks all six, so Import throws them away. The rt.cs harness found that the normal save/load round trip keeps all 46 keys, and that Export→Import drops exactly `mood crlrev buddy outagebest hat ovat`. Add them to Known; hat and outagebest are already validated by Cfg.Read.  
Confidence: confirmed (ran it: rt.cs set non-defaults, Lines(), Read back, diffed with Known)

### 5. Settings search finds nothing for "Wi-Fi", "sign-in" or "français"
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 167-173 (SetMatch splits the setting text on `[^a-z0-9.]+` but keeps the query words whole)  
How to reproduce: Settings › search box: type `Wi-Fi` (the app's own spelling), `sign-in`, or `français`.  
What happens / what should happen: "Nothing matches". The settings text "Wi-Fi watch" splits into "wi" and "fi", but the query stays the single word "wi-fi", so nothing starts with it. "Sign-in page check" fails the same way for `sign-in`. `français` is literally in the Language row's Keys, but "ç" splits it into "fran" and "ais". The sm2.cs harness over the real 38 rows gave: wi-fi 0, Wi-Fi 0, sign-in 0, français 0, while wifi 2, ping 7, bgp 1. Split the query with the same regex, and keep letters beyond a-z (`\p{L}`).  
Confidence: confirmed (ran it: sm.cs and sm2.cs via reflection on SetItems()/SetMatch)

### 6. Minimising the window (or Win+D) stops a running 60-minute MTR, ISP report or any other running tool
Severity: wrong result  
Where: src\Ui\FullView.cs line 360 (Resize → Minimized → BeginInvoke(Close)), line 371 (FormClosed: `tbRun0.Stop = true`), against lines 322-323 (Esc is deliberately ignored while tbBusy)  
How to reproduce: Toolbox › MTR, Minutes = 60, Run. Press Win+D (show desktop), or click the minimise button, to get to another window.  
What happens / what should happen: the view closes and the MTR is stopped. Its result, the hop table and the Compare are lost (the partial CSV is written but nothing points to it). The ISP report, iperf3, sustained ping and the rest go the same way. Esc is guarded because "a stray Esc doesn't throw it away", but minimising is far more common and isn't guarded. While tbBusy (or sniffing), it should hide to the corner without closing, or ask, or say first that minimising stops it.  
Confidence: suspected (read)

### 7. Health page: the "DONE hh:mm" stamp shows the current time, not when the check finished
Severity: wrong result  
Where: src\Ui\FullView.Tools.cs line 59 (`"  ·  DONE " + DateTime.Now.ToString("HH:mm")`)  
How to reproduce: Investigate › Health › e.g. Background jobs. Wait a few minutes and scroll or hover so the page repaints.  
What happens / what should happen: the header says DONE at whatever minute the repaint happens, so it keeps moving. It should use toolAt, which is set when the check finishes and is already used for Copy and the evidence pills.  
Confidence: suspected (read; deterministic)

### 8. Compare file vs live server rejects a pasted URL that every neighbouring tool accepts
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 275 (certlive is in UrlTools, so its Server box is never turned from a URL into host:port); src\Checks\CertKeys.cs line 249 (Lab.Split)  
How to reproduce: Toolbox › Compare file vs live server, Certificate = any .crt, Server = `https://vpn.example.com:8443/`.  
What happens / what should happen: "type the server - e.g. vpn.example.com or 10.0.0.5:8443". `Lab.Split("https://intranet.example.com:8443/login")` returned false in the harness. The tool is in UrlTools to protect its file box, but that also switches off the URL rewrite for the server box. UrlTools works per tool; it needs to work per box: skip box 0 only, or handle URLs in Live like Https does (CertChain.cs line 293).  
Confidence: confirmed (ran it: url.cs, Lab.Split on the URL)

### 9. The open window doesn't take up imported settings (mode tabs, Toolbox view, Points button, language)
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 547-559 (ImportSettings: Cfg.Read, Policy.Apply, Save, Nudge, BuildPinBar only)  
How to reproduce: export a settings file with mode=pro, tbview=list, points=0 and lang=fr. Then, in a Simple-mode window, Import it.  
What happens / what should happen: the Settings rows now show Pro, Off and Français, but the tab row is still Simple (ApplyMode isn't called), the Adeebo button is still there (SyncPointsBtn isn't called), the Toolbox is still "by question", and the language isn't loaded (L.Use isn't called). Each Settings row's own setter does these things. Import should call the same setters, or say "restart to apply".  
Confidence: suspected (read)

### 10. live_capture = off: Start live capture does nothing and says nothing
Severity: wrong result  
Where: src\Ui\FullView.cs lines 241-247 (liveBtn.Enabled = LiveCapture.Available only), 542-548 (ToggleLive), 243 (label text); src\Capture\LiveCapture.cs line 81 (Start returns false, only logs)  
How to reproduce: policy `live_capture = off`, Wireshark installed. Monitor › Live capture › Start live capture.  
What happens / what should happen: nothing happens. The page keeps saying "Press Start live capture" and "Off until you press Start". Sniff and the Journey both show "turned off by your IT team" (FullView.cs line 948, Journey line 187). Live capture should show the same message and disable the button.  
Confidence: suspected (read)

### 11. Toolbox: clicks that open another tool are silently dropped while a tool runs, and some of them write into the running tool's boxes
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs line 664 (SelectTool: `if (tbBusy) return;`), line 257 (ShowSection overwrites run.Status); src\Ui\FullView.Overview.cs line 49 ("Add my own server..." → OpenTool("speedtest") then `tbBoxes[0].Box.Text = "list"`); src\Ui\FullView.cs line 204 (Check a destination chips)  
How to reproduce: start an MTR (1 min). Then (a) on the Overview, open the server chip › Add my own server…; (b) on Check a destination, click "MTU per hop"; (c) click another Toolbox section.  
What happens / what should happen: (a) the MTR stays open and its Destination box now reads "list", so it no longer matches the run on screen. (b) the click is ignored with no word. (c) the run's real status ("Probing every hop once a second...") is replaced by "still running - press Stop first to change section" for the rest of the run, because most tools set Status only once. A refused switch should say so without clobbering the run's status, and the Overview should check tbBusy before it writes into the boxes.  
Confidence: suspected (read)

### 12. Toolbox search: "wifi" misses most Wi-Fi tools, "SPF" opens the routing-protocol listener, "dmarc" suggests "mac"
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs lines 553-568 (Synonyms), 787-814 (Matches: substring `text.Contains(w)`), 816-824 (DidYouMean)  
How to reproduce: Toolbox search, as run by the m.cs harness on the real Tools2() and Explain terms: `wifi`, `wi-fi`, `SPF`, `dmarc`, `port 443`.  
What happens / what should happen: `wifi` gives 3 tools (roams, dhcp, arp). The search box's own hint says "wifi", but "Is this Wi-Fi OK to use?", "Wi-Fi rush hour" and "Side by side: Wi-Fi vs cable" aren't among them, while `wi-fi` gives 19. `SPF` gives only rlisten, because "spf" is a substring of "OSPF". `dmarc` gives nothing and "Did you mean mac". `port 443` gives only udpmtu, not Port test. Map wifi to the same tools as wi-fi (or normalise the hyphen), match explanation text on word starts rather than any substring, and add spf/dkim/dmarc → smtppath / dig (still missing from round 1).  
Confidence: confirmed (ran it: m.cs, en and fr)

### 13. Settings › Keep history for highlights the wrong length under an IT policy
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs line 145 (Cur = FindLastIndex(choices <= HistoryDays)); src\Policy.cs line 69 (history_days accepts any 1-180)  
How to reproduce: policy `history_days = 5`.  
What happens / what should happen: Cfg.HistoryDays is 5, but the row highlights "3 days". Clicking "7 days" stores min(7, 5) = 5 and still shows "3 days". Mark the row as policy-held (PolicyLocks), or show the real value.  
Confidence: suspected (read)

### 14. What changed repaints every second; Network story every 2 s; a running tool's canvas twice a second
Severity: slow  
Where: src\Ui\FullView.cs line 519 (`if (tab == "changes") changesCanvas.Invalidate();` on every Monitor version, i.e. every second, unkeyed), line 582 (travelCanvas.Invalidate on every connection refresh, every 2 s on the travel tab), line 525 (tbCanvas.Invalidate every 500 ms tick while tbBusy), lines 871 and 894 (UpdateBadges → PageBadge("changes") builds a list of up to 300 events, twice per 500 ms tick, on every page)  
How to reproduce: open Time machine › What changed and leave it; or run Routing protocols on my segment (120 s) or a 60-minute MTR and watch the paint count (the DB slow-paint log only shows paints over 40 ms).  
What happens / what should happen: the What changed page (insights, outage log and up to 300 event rows) repaints every second whether or not an event arrived. Network story with no target repaints every 2 s. A Listen run, where nothing changes for 120 s except the status, repaints 240 times. A 60-minute MTR repaints 7,200 times, each one drawing up to 3,600 series bars and copying the series list. The other timer paths already use a key (headKey, pathKey, sideKey, footKey, ovKey, gamerKey): key these on M.Events' newest time, connSnap's version, run.Status plus the series count, and cache the "changes" badge.  
Confidence: suspected (read; timer paths counted: header/path/side/footer/overview/gamer keyed; changes, travel, tbCanvas, destCanvas-while-running, liveCanvas unkeyed)  
Rule: 7

### 15. Widget cards create a new GDI font on every draw
Severity: slow  
Where: src\Ui\Theme.cs line 425 (GamerCard: `new Font(fHead.FontFamily, 26 * S, …)`), line 489 (MetricCard: calls, engineer), line 321 (Buddy asleep: `new Font("Segoe UI", …)` per chip draw)  
How to reproduce: Widget style Gamer while playing, or hover the Calls or Engineer card. The card redraws every second while hovered or playing.  
What happens / what should happen: one font is created and disposed per draw, roughly 3,600 an hour while gaming. The class comment says "All fonts made once; nothing is created per paint". Make the 26 px font and the "z" font once in Draw.Init.  
Confidence: suspected (read)  
Rule: 7

### 16. Switching from dark to light leaves the Toolbox safety tick's text near-white on white
Severity: cosmetic  
Where: src\Ui\FullView.cs lines 557-570 (Recolor: only TextBox and Label get a ForeColor); src\Ui\FullView.Toolbox.cs line 643 (tbSweepOk created with ForeColor = Theme.Ink)  
How to reproduce: dark theme, open the Toolbox (it's also built 2.5 s after the view opens), switch to light with the header switch, then open Ping sweep or any Lab tool.  
What happens / what should happen: "This is my own network, or I'm allowed to send test traffic to it" is drawn in #E6EDF3 on white, so it's practically invisible. This is the safety statement the user is supposed to read before ticking. The rc.cs harness: after Apply(dark) → Apply(light) → Recolor, the checkbox fore was #E6EDF3 and its back was White. Recolor should handle ButtonBase or CheckBox (ForeColor = Ink).  
Confidence: confirmed (ran it: rc.cs under xvfb/mono)

### 17. High contrast: switching it on or off while running isn't followed, and BlueSoft panels lose their text
Severity: cosmetic  
Where: src\Ui\Theme.cs lines 48-76 (Apply: the high-contrast override only maps Ink, Muted, Card, Line, RowSel and BlueDark, not BlueSoft or Blue); src\AppCtx.cs lines 139-144 (re-applies only `if (d2 != Theme.Dark)`)  
How to reproduce: (a) with AdeeboMeter running, turn on a Windows contrast theme (the light/dark app mode stays as it was). (b) Start in a contrast theme where WindowText is white while the app mode is light. Look at the Overview "Sounds like…" hint and the first-run Simple/Pro card.  
What happens / what should happen: (a) nothing changes until restart, because UserPreferenceChanged only re-applies when dark/light flips (and turning the contrast theme off keeps the system colours). (b) those panels are filled with BlueSoft #EEF4FF and their text is Ink = WindowText (white): white on near-white (FullView.Overview.cs lines 222-224 and 312-313). The tip bar, the selected Settings preset card and the About heading discs are BlueSoft fills too. Re-apply on any UserPreferenceChanged (or on HighContrast changing), and map BlueSoft to SystemColors.Window under high contrast.  
Confidence: suspected (read)

### 18. Settings rows with many choices spill over the label and the side menu on a 1366×768 laptop at 125 %
Severity: cosmetic  
Where: src\Ui\FullView.Settings.cs lines 282-316 (SetRow: `tw = w - ow - 24*S` may go negative; the segment strip is drawn at `x + w - ow`), line 182 (w)  
How to reproduce: a maximised view at 1366×768, 125 % scaling (the most common laptop setup). Settings › History & data, General (Voice) and Alerts (Monthly data cap). French is a little worse.  
What happens / what should happen: measured with Liberation Sans (Arial metrics, close to Segoe UI) at S = 1.25, w = 569 px: "Keep history for" segments are 620 px wide (help column -80 px), Voice 519 px (help column 21 px), Monthly data cap 496 px (44 px). The strip starts left of the row's x, over the label and up to the side menu (whose clickable areas then overlap), and the help text gets a width ≤ 0 or about 2 characters per line. At the minimum window size (980 px, 100 %) HistDayNames is -150 px, Voice -69 and Cap -52. When `ow > w * 0.6`, wrap the segments onto a second line under the label.  
Confidence: confirmed (ran it: seg.cs with the real option arrays, translated through L.T, and FullView's width formula; widths approximate the Windows font)

### 19. Settings and About texts that are no longer true
Severity: cosmetic  
Where: src\Ui\FullView.Settings.cs line 127 (Wi-Fi watch: "The priciest job here - it starts a small Windows tool (netsh) each time"), line 328 (About: "from Penny to Engineer"), line 111 (Language help: "les noms des outils … restent en anglais"), line 227 and 33 (Penny "Wi-Fi every 2 min", Wi-Fi row `Every(Cfg.WifiSec)`), line 392 (About Wi-Fi row)  
How to reproduce: read Settings › Background checks › Wi-Fi watch, About › Getting around, and Settings › General › Language in French.  
What happens / what should happen: since c529c2b, Wi-Fi watch asks the WLAN service directly and netsh is only the fallback. The Engineer preset was merged into Balanced (4fd6e22); the presets are Penny, Balanced and Gamer. All 163 Toolbox titles are now in the French table (fr2.py found 0 untranslated). And with the window open, Wi-Fi is read every 10 s whatever the preset (Monitor.cs line 223 `ViewOpen ? 10`), while the row, the Penny card and About say "every 2 min" or "every 30 s". Update the wording, and add "(every 10 s while this window is open)" as the router row already does.  
Confidence: suspected (read)

### 20. French: untranslated captions and one overlap measured in English
Severity: cosmetic  
Where: knowledge\lang-fr.txt; src\Ui\FullView.Toolbox.cs line 991 (the award is placed after `tbTitle.ToUpperInvariant()` measured in English, but the title is drawn as L.T(tbTitle)), lines 620-621, 679-681 (RoundBox.Label/Cue set without L.T); src\Ui\FullView.cs lines 158, 194, 268 (cues), 219-228 (Health chips), 882 ("Your cable"/"Cable")  
How to reproduce: Settings › Language › Français, restart. Then open the Toolbox, a tool, and Investigate › Health. fr.py extracts every L.T("…"), `new PillButton("…")` and `new RoundBox(…, "…")` literal from src and diffs the table.  
What happens / what should happen: 25 literals are missing from the table. Real UI ones: "By question", "All tools (list)", the "Adeebo" header button, and the cues "Filter by app, host, IP or port", "host, IP or URL - e.g. github.com:443", "Find a process or app", "Site, app or server - e.g. outlook.office.com", "Find a process", "Search: DNS, TCP, svchost, jitter...", "Jump to a tab, program, term or protocol...", "Find a feature... mtr, dns, wifi, certificate", "Your website or mail domain (optional)", "Or type a site - e.g. github.com", "Room name - e.g. Kitchen", plus GameEditor's 6. Captions built from arrays are missing too: 11 Health chips ("Pre-call check", "DNS speed test", "VPN map", "What can AdeeboMeter see here?", "Memory, disk, heat", "Disk", "Background jobs", "Crashes and hangs", "Graphics per app", "What changed (7 days)", "Battery") and the tab names "Your cable" and "Cable". Every Toolbox box label and cue (set after construction, so never through L.T), the SNMP view tabs, the group headers, "Copy as text", "Save...", "Takes … Press F1", the safety tick and "DONE" stay English. On a tool page, the green "+N Adeebo Points" is drawn at the English title's width, so it overlaps longer French titles: "dig - any DNS record" is 1.95× longer in French. "Play again" is listed twice (line 983, same value).  
Confidence: confirmed (ran it: fr.py / fr2.py over src and lang-fr.txt)

### 21. Keyboard users never earn the "used a page" point on painted-only pages
Severity: cosmetic  
Where: src\Ui\FullView.cs line 793 (WireKeys Press: runs the action, no PageUsed), against line 812 (mouse click: `PageUsed(usedTab)`)  
How to reproduce: Tab to a painted action on What changed, About or Network story (pages without real buttons) and press Enter.  
What happens / what should happen: the action runs, but "Use a page for the first time" is never awarded. A mouse click on the same spot awards it. Press should call PageUsed(tab) like the click path.  
Confidence: suspected (read)  
Rule: 10 (adjacent: earning differs by input method)

## Still present from earlier rounds

| earlier id | where now | evidence |
|---|---|---|
| Muse-200 #15 (MTR Duration clamps at 10 min, tool allows 60) | FullView.Toolbox.cs line 501 vs line 32 | `Math.Min(10, n)` in Duration, `Math.Min(60, min)` in the tool: "30" says "10 minutes". |
| Muse-200 (TWAMP has no Duration case) | FullView.Toolbox.cs lines 488-547 | no `case "twamp"`; 100 packets say "a few seconds". |
| R1 (no spf/dkim/dmarc search words) | FullView.Toolbox.cs lines 553-568 | `SPF` → rlisten via "OSPF", `dmarc` → "Did you mean mac" (finding 12). |
| R1 (Language switch applies at once to painted text, not to captions) | FullView.Settings.cs line 111 | `L.Use(Cfg.Lang); Invalidate(true)` while PillButton captions keep the old language. |
| Muse-200 #143 (Import with autostart= opens Store Startup settings) | FullView.Settings.cs line 551 | `if (k == "autostart") Cfg.SetAutoStart(v == "1")` on any imported value. |
| R1 #262 / #1201 (HWND_TOPMOST on every Present) | Controls.cs line 60 | SetWindowPos(-1) on every widget draw. |
| R3-new #16 (Console example) | knowledge\examples.txt line 852 | Fill was removed (fixed); the example still prints "PC → router 3 ms (192.0.2.1)", which the tool can't print. |
| R1 #1401 (duplicate French key) | lang-fr.txt line 983 | now "Play again" (the "Simple" duplicate is gone). |
| R1 (UrlTools per tool, not per box) | FullView.Toolbox.cs line 275 | the dig/dnscmp DoH case is fixed by the label check; certlive/certcmp are still wrong (findings 3 and 8). |

## Checked and held up

- Example registry against the tool registry (ex.py): 163 tools in Tools2(), 163 in TbLayout with no stray or duplicate ids, 164 examples (163 plus page.gamer). Every tool has one, no example has more inputs than its tool has boxes, and all levels are in -1..3. Every Fill value is documentation-range (192.0.2/24, 198.51.100/24, 203.0.113/24, 2001:db8::), example.*, a calculator value or a local path. The secret boxes (community, shared secret, password, code) are skipped by Fill (UseSystemPasswordChar).
- Rule 9: Ctrl+C, the Copy link, Save, Compare and tbLast all refuse example rows (IsExample checks at Toolbox lines 331, 414, 444, 995-997, and tbLast is only written by real runs). Example rows carry no row actions apart from the banner's exfill.
- Rule 4: ResultText/ResultCsv drop secret rows ("k") and secret input labels. tbShown blanks secret boxes, and httpreq's auth or cookie headers and its body are hidden before tbLast or Copy.
- Every Questions card covers its TbLayout groups, and every group belongs to exactly one card. LabToolsMissingGate is consistent with LabGate, SenderGate and Senders.
- URL stripping: dig's and dnscmp's DoH server boxes are left alone (the label contains "https://" / "DoH"); payload and secret boxes are skipped; host:port boxes keep the port.
- Toolbox search with regex characters (`(`, `.*`, `[a-`, `\`), an empty query and upper/lower case: no exception (Regex.Escape, ToLowerInvariant). "speed" → speedtest first, "printer"/"printers" → snmpprn, "cert" → certs, "ssl" → https (the round 1-2 ordering issues are fixed). French search works on translated titles ("vitesse", "imprimante", "pare-feu").
- Icmp.Resolve4 with 31 hostile inputs (brackets, %, NUL, file:, javascript:, 300 chars, bad IPv6): no exception.
- Settings round trip: all 46 keys written by Cfg.Lines() come back through Cfg.Read with the same values. Every Settings option array lines up with the values Cfg.Read accepts. Saved preset=engineer opens as balanced and ApplyPreset runs at load. The rows locked by policy (Mode, Internet and DNS, BGP and its timer, the four Gamer rows, Live capture) get a no-op setter and the "Set by your IT team" help; history days and revocation clamp to the policy; preset cards re-apply Policy.Apply.
- BeginInvoke after close: every background completion (RefreshConns, StartDest, StartHealth, RunTool, StartTool, StartMtu, MeasureHops, revocation, weather, closest server, speed) is wrapped and resets its busy flag in the catch. The watchdog is disposed in FormClosed, and the warm-up timer checks IsDisposed. Double-starting a Toolbox run isn't possible: Run, Enter in a box, list Enter and RowAction all check tbBusy on the UI thread, and StartTool sets it synchronously.
- LiveCapture's percentages are written and parsed with the same CurrentCulture (Paint.cs lines 426 and 457), so French decimal commas don't throw.
- The DB focus mark (ActsInOrder sort) runs only when the panel is focused with focus cues, not on every paint. PillButton re-announces its name when its text changes. ConnList has arrow, Page, Home, End and Enter keys.
- No Nokia references in any S7 file.
