# P1-core - release check of 1.1.6 (999a100)

Slice: top-level src\*.cs except SelfTest, BugReport, Relay, Weather; src\Apps, src\Brain, src\Knowledge; knowledge\lang-*.txt and lines-*.txt.
Harnesses: scratchpad\review5\P1-core\ (langcheck.py, linescheck.py, h\q.cs, h\w.cs, h\f.cs, h\m2.cs, fresh\ for the --report runs).

### 1. [day 1] Closing AdeeboMeter before the first-run questions are answered turns the next start into Pro mode with a "My setup" view nobody made
Severity: wrong result  
Where: src\AppCtx.cs line 262 (LoadSettings: `if (!first && Cfg.Mode == "") Cfg.Mode = "pro"`) and line 268 (`Views.EnsureStarted()` for every non-first start), src\Views.cs lines 162-167 (EnsureStarted → Make("My setup")); src\Ui\FullView.SetHome.cs lines 148 and 169 (Skip and the last answer only fall back to Simple when Mode is still "")  
How to reproduce: a brand-new PC. Start AdeeboMeter (installed or run here). Don't open the window, or open it and close it before answering the first question. Quit from the tray, or restart Windows (with Start with Windows on). Start it again.  
What happens / what should happen: the first start saves a full settings file (`mode=`, `view=`, `setupdone=0`, `setupstep=0`, 98 lines). On the second start `first` is false, so the code treats it as an upgrade from 1.1.4: Mode becomes "pro" and a custom view "my-1 / My setup" is created in Pro with every tab. The setup card still shows (SetupDone is false), but pressing Skip or answering the last question keeps Pro, because the fallback to Simple runs only when Mode is "". The made-up "My setup" view stays in the view list for good. Treat a file with `setupdone=0` as a fresh install, not an upgrade (only a file with no `setupdone` key comes from 1.1.4), and don't call EnsureStarted while SetupDone is false. This is round-4 #16 in a new form: that fix only covered the installer's autostart-only file.  
Confidence: confirmed (ran it: h\m2.cs runs AppCtx.LoadSettings twice on an empty folder. Start 1: Mode '' View '' SetupDone False. Start 2: Mode 'pro' View 'my-1' views=`my-1~My setup~pro//balanced/...`)

### 2. [day 1] Offline in Spanish, Arabic, Bengali or French, the buddy says "Your router isn't answering" instead of "Not connected to any network"
Severity: wrong result  
Where: src\Mood.cs line 124 (Quip: `s.Headline.IndexOf("not connected to any network", OrdinalIgnoreCase)`), against src\Monitor.cs line 883 (the headline goes through L.T)  
How to reproduce: Settings › Language = Español (or ar, bn, fr). Turn Wi-Fi off / unplug the cable. Look at the buddy line on the widget card or the Home header.  
What happens / what should happen: the line key is picked by looking for the English words in the headline. The headline is now translated ("Sin conexión a ninguna red..."), so the check fails and the key becomes `gw.down`. The buddy tells an offline user to restart the router or check the cable, while the headline says there is no network. English correctly gets `net.none`. Pick the key from the state (offline flag / Path), not from the headline text.  
Confidence: confirmed (ran it: h\q.cs. en: "Offline. Nothing can reach the internet right now."; es: "Sin respuesta de tu router. Reinícialo o revisa el cable."; fr: "No reply from your router. Restart it or check the cable."; ar and bn also give their gw.down line)

### 3. With the internet check off (Settings or IT policy), every wake from sleep logs an amber "DNS didn't answer within 2 minutes", and the wake-up report says DNS failed
Severity: wrong result  
Where: src\Monitor.cs line 277 (ProbeDns runs only inside the `netSec > 0` block), lines 538-551 (WakeStep: on the 2-minute timeout, `dn` is NaN → level 1 "· DNS didn't answer within 2 minutes"); src\Checks\OverTime.cs lines 236-246 (WakeUp counts those as "DNS never answered")  
How to reproduce: Settings › Internet and DNS check = Off (or `internet_check = off` in AdeeboMeter.policy). Put the PC to sleep and wake it. Wait 2 minutes. Look at What changed / Time machine, then Toolbox › wake-up report.  
What happens / what should happen: the router probe calls `WakeStep(t0, gwOk, false)`. DNS is never asked, so after 120 s the event "Back online after waking: router 3 s · DNS didn't answer within 2 minutes" is logged at level 1. It goes into history, and the wake-up report then lists "DNS never answered: N times" (amber) for every wake. On a company PC with the policy, every morning gets a false amber DNS finding. When the internet check is off, end the wake report on the router alone and say DNS wasn't checked.  
Confidence: confirmed (ran it: h\w.cs: Resumed(), WakeStep(+3 s, router ok, dns false), WakeStep(+125 s) → `lv 1 [wake] Back online after waking: router 3 s · DNS didn't answer within 2 minutes`; DNS-only-when-netSec>0 read at line 277)

### 4. In any language but English, a Wi-Fi roam during a call is never named in the call summary, and the Time machine doesn't spark on it
Severity: wrong result  
Where: src\Monitor.cs line 641 (the roam event text is `Voice.P("Wi-Fi hopped to another access point", "Wi-Fi roamed to another access point")`, which now goes through L.T); src\Brain\Watch.cs line 82 (Calls.Summarise looks for "roam" / "hopped" in the text); src\Ui\FullView.Replay.cs line 277 (looks for "access point")  
How to reproduce: Language = Español/Français/العربية/বাংলা. Be in a Teams call while the laptop roams to another AP and the router delay goes over 80 ms. End the call.  
What happens / what should happen: both texts are in every table (es "El Wi-Fi cambió a otro punto de acceso", fr "Le Wi-Fi est passé…", ar, bn). The event is stored translated, so the English substring tests never match. The summary falls through to a generic line and loses "Audio likely broke at HH:mm - Wi-Fi roamed mid-call", and the replay doesn't spark on the hop. The same thing will happen to any future test on event text. Mark roam events by kind (for example "wifi-roam") or a flag, not by words.  
Confidence: confirmed (read the code; confirmed by table grep that both keys are translated in all four tables)

### 5. The Arabic table has machine-translation errors that change the meaning, including the Example banner and IT-policy text
Severity: wrong result  
Where: knowledge\lang-ar.txt (its header says "Native Arabic proof review is required"); also a few in lang-bn.txt  
How to reproduce: Language = العربية, then open any tool's Example, Settings › online sending under a policy, the hash compare, Learn.  
What happens / what should happen: some of what Arabic users read is wrong, not just clumsy:
- "Made-up data - not your PC or network. Nothing was sent." → "البيانات المختلفة…" ("different data"). The Example banner no longer says the data is made up (rule 9: examples must be clearly marked).
- "Off - your IT team turned online sending off." → "لقد أطلقت فريق تكنولوجيا المعلومات على الإنترنت" ("you launched the IT team on the internet"). The meaning is reversed.
- "does NOT match what you pasted" → "لا يطابق ما قمت بتثبيته" ("what you installed").
- "nobody yet - a CA signs it" → "…وقعت على القانون" ("signed the law").
- "RAID capacity" → "قدرة المداهمة" (a police raid). "Multicast…" → "التذاكر المتعددة" ("multiple tickets"). "QoS and DSCP" → "القواعد القياسية والمركز المركزي". "SNMP - walk any OID or name" → "المشي أي شخص" ("walking any person").
- "The UPF" → "الاتحاد الوطني للطاقة النفسية" ("national union of psychic energy"). "The MME" → "الشركات الصغيرة والمتوسطة" (small businesses). "LIVE"/"live" → "على قيد الحياة" ("alive").
- "Turn on" → "إشغلي" (misspelled, feminine). "How would you like AdeeboMeter?" → "كيف تحبين \"إدبوبوميتير\"" (feminine, and the name is misspelled). "TERMS USED IN ADEEBOMETER" → "…ADEBEOMETER". "Teams" → "الفريقات".
- bn: "drawn"/"Drawn" → "স্ক্রিন দেখানো হয়েছে।" ("screen shown.").

68 Arabic lines lose a technical acronym (scratchpad P1-core, acronym scan). Don't ship Arabic labelled "100% translated" before a native pass on at least the safety, policy, Example and security strings.  
Confidence: confirmed (parsed every table with langcheck.py and an acronym and length scan; the quotes above are copied from the table)

### 6. French says "100% translated", but about 1,300 strings that Spanish, Arabic and Bengali translate stay English, and the buddy only speaks English
Severity: wrong result  
Where: knowledge\lang-fr.txt line 2 (`#coverage 100`), src\Lang.cs lines 76-94 (Coverage reads that line), src\Ui\FullView.Lang.cs line 283 ("100% translated"); src\Lines.cs line 180 (packs exist only for es, ar, bn: no lines-*-fr.txt)  
How to reproduce: compare the tables: es/ar/bn have 5,380 keys, fr has 4,095. 1,286 keys that es has are missing from fr, for example "Server", "OK", "Teams", "noted", "The postman", "Asks DNS again", "Wait - it happens on its own every few minutes.". These are the variable-reached texts of Learn games, labs and Toolbox. Then pick Français and read any buddy line.  
What happens / what should happen: tools/langcheck.py counts only `L.T("literal")` strings (2,863), so fr gets 100% even though about a quarter of what the other tables cover is English in French. The language picker tells French users "100% translated". Every persona and mood line falls back to the English pack (h\q.cs: fr → "No reply from your router. Restart it or check the cable."). Measure coverage against the union of keys in the most complete table, or say "partly translated" for fr, and say the buddy speaks English.  
Confidence: confirmed (ran it: langcheck.py on a copy of the tree for each language; key diff es vs fr = 1,286 missing, 1 extra; the h\q.cs run above)

### 7. [day 1] "Just run it from here" starts a Downloads copy with Windows by default
Severity: wrong result  
Where: src\Setup.cs line 315 (`autoStart = Setup.InstalledAutoStart(instExe)` → true when nothing is installed), lines 497-502 (the "here" button writes `autostart=1` next to this copy); src\AppCtx.cs lines 41 and 283-296 (ApplyAutoStart writes the Run key for this exe)  
How to reproduce: download AdeeboMeter.exe to Downloads, double-click it, leave the switch as it is (on), press "Just run it from here". Check HKCU\...\Run.  
What happens / what should happen: the switch sits between the three install promises and the Install button and is on by default. The "here" path honours it, so `AdeeboMeter = "C:\Users\…\Downloads\AdeeboMeter.exe"` is added to the Run key. A file in Downloads then starts at every logon, and it keeps pointing there after the user deletes it. The code's own rule (AppCtx line 41) is "a downloaded or portable copy never starts with Windows unless you ask". Default the switch to off when "here" is pressed, or show it only for Install.  
Confidence: suspected (read it)

### 8. After "Just run it from here" from Downloads, no later download ever offers to install or update, and the settings never carry into an install
Severity: wrong result  
Where: src\Setup.cs line 73 (ShouldOffer for a Downloads copy: offer only when %LOCALAPPDATA%\AdeeboMeter\AdeeboMeter.settings doesn't exist), lines 160-180 (Install copies settings only from the Run key's folder or from Base, never from LocalDir); src\Program.cs line 78 (a Downloads copy keeps its settings in LocalDir)  
How to reproduce: run a Downloads copy once with "Just run it from here", pick Español, answer the setup. Later download 1.1.7 into Downloads and double-click it.  
What happens / what should happen: every Downloads copy shares the same LocalDir settings file, so the offer never appears again for any version. 1.1.7 simply takes over and runs from Downloads, and there's no way to install from the app. If the user does get an install (by deleting the file), the installed copy starts fresh: English and the first-run questions, because Install never looks in LocalDir. Offer again when the running exe's version is newer than the one that wrote the settings, and copy LocalDir's settings on Install.  
Confidence: suspected (read it)

### 9. Waking from sleep with DNS down: a DNS lookup and a ping to 1.1.1.1 every second for 2 minutes, on any preset
Severity: slow  
Where: src\Monitor.cs line 241 (`netSec = … Waking && !NaN(wakeGw) ? 1`), line 277 (`|| (Waking(t0) && !double.IsNaN(wakeGw))` → ProbeDns on every pass), lines 454 (a failed lookup sleeps 2 s and asks a second name)  
How to reproduce: Penny preset. Sleep the PC, wake it on a network whose router answers but whose DNS doesn't (captive portal, DNS server down).  
What happens / what should happen: until DNS answers or 120 s pass, the probe loop runs every second. Each pass pings 1.1.1.1 and does two uncached DNS lookups (www.microsoft.com, then www.cloudflare.com after 2 s), so dozens of each go out in 2 minutes. About and the Settings help say "one name lookup at most once a minute", and Penny is sold as the lightest touch. Back off (for example 1, 2, 4, 8 s) during the wake window.  
Confidence: suspected (read it)

### 10. A saved or imported view can set intervals the Settings lists never allow (internet ping every 2 s, game pings every second)
Severity: slow  
Rule: 7  
Where: src\Views.cs line 36 (State.Parse accepts any 0-3600 for gw/net/wifi/apps/gameBg), line 76 (Put writes them to Cfg without the `Pick` lists Cfg.Read uses, src\Program.cs lines 454-457, 510); `views` is in Cfg.Known, so Import settings takes it  
How to reproduce: a settings file (or an imported one) with `views=everyday~Everyday~simple//balanced/auto/0/1/1/1/1/1/1/5` and `view=everyday`; start, or switch views.  
What happens / what should happen: Cfg.NetSec = 1 and GameBgSec = 1. The probe loop's floor is 2 s, so it pings 1.1.1.1 every 2 s and, once a game is watched, 5 game servers every second. Cfg.Read would have refused all of these. Validate State values against the same allowed lists.  
Confidence: suspected (read it)

### 11. "Why is Outlook slow?": the Internet row is green with "-" when nothing was measured
Severity: wrong result  
Where: src\Apps\M365.cs OutlookSlow, "The internet" row (`!double.IsNaN(net) && net > 150 ? "1" : "0"`)  
How to reproduce: in the first seconds after start, with the internet check off, or under the policy: open the Outlook check.  
What happens / what should happen: `NetPing.Avg(10)` is NaN, the value shows "-" and the level is 0 (green, OK). Nothing was measured, so it should be level 3 (unknown) with "not checked". (The shared `NotRunning` array is also handed out by reference; a caller that edits a row in place edits every later answer.)  
Confidence: suspected (read it)

### 12. A day's view in the Time machine keeps the first 2,000 events of the day, not the last
Severity: wrong result  
Where: src\History.cs line 186 (Load: `Lines(FileFor(d, "ev"), 2000)` reads from the start of the file); EventsOf (lines 151-170) was fixed for this in round 6, Load wasn't  
How to reproduce: a day with more than 2,000 events (a flapping VPN, a device-heavy LAN after a sweep, Wi-Fi notes all day). Open that day in the Time machine in the evening.  
What happens / what should happen: everything after roughly the first 2,000 lines (the afternoon and evening) is missing from day.E, so the evening's red minutes have no "what changed" next to them. Read the whole day, bounded, then keep the problems and the newest, as EventsOf does.  
Confidence: suspected (read it)

### 13. Text in the 5 new languages: the setup window, tray balloons, alerts, the "Busy" chip, the IT-policy label and the buddy's extras stay English
Severity: cosmetic  
Where: src\Setup.cs lines 403-468 (SetupWindow: every string is English; it also runs before L.Use, so Uninstall and Update are English for every user); src\AppCtx.cs lines 157 and 633 (crash and felt-slow balloon titles), src\Brain\Watch.cs lines 40-44, 96, 133, 196 and src\Brain\Brain.cs lines 161 and 246 (every Alerts.Raise text is built by concatenation and never translated); src\Monitor.cs line 944 (`level == 1 ? "Busy"`: no L.T, drawn raw at src\Ui\Theme.cs line 692 while "Needs you" and "All quiet" are translated); src\Policy.cs line 33 (Policy.Locked is in no table); src\Knowledge\Explain.cs lines 36-42 (CloseText: no L.T, not in any table); src\Mood.cs lines 132-181 (koans, provider excuses and season lines English inside a Spanish or Arabic buddy); src\Cases.cs line 177 (search words English only, so "lento" or "تقطيع" never opens a case); the timeline events in src\Monitor.cs and src\Brain\Watch.cs (concatenated, so they can't be translated)  
How to reproduce: Language = العربية. Let the PC get busy (CPU over 60%): the widget chip says "Busy". Get a Wi-Fi drop alert, or a call summary pop-up. Run Settings › Apps › Uninstall.  
What happens / what should happen: these are the first things a new user sees (corner widget, notifications, installer). Wrap the static ones in L.T, build the alert texts from format templates, and call L.Use before the SetupWindow (read `lang=` from the settings first).  
Confidence: confirmed (read it; grep shows "Set by your IT team (AdeeboMeter.policy)." and "Safe to close. It's an app…" missing from all four tables, and "OK" missing from fr)

### 14. The Remove window says "its settings and history go", but Adeebo Points, your own buddy lines and the rest of the data folder go too
Severity: cosmetic  
Where: src\Setup.cs line 460 (AskRemove text), lines 240-248 (everything in %LOCALAPPDATA%\AdeeboMeter is deleted except keys-ec.txt, recipients and certs: points.txt, lines\mine.txt, moments, usage, baselines, devices, the Hall of Fame card's points…)  
How to reproduce: earn points, write your own lines, then Uninstall and reinstall.  
What happens / what should happen: the total (rule 10: "survives a restart") and the user's own lines are gone with no warning. A user with 480 points who reinstalls to fix something starts again from 0. Name them in the Remove text, or keep points.txt and lines\ like the keys.  
Confidence: suspected (read it)

### 15. French (and any language with no buddy pack) loads buddy lines from a "knowledge" folder next to the exe, including one in Downloads, with no cleaning
Severity: security  
Where: src\Knowledge\Knowledge.cs lines 47-53 (RawText falls back to `<exe dir>\knowledge\<pack>.txt` and `<exe dir>\..\knowledge\<pack>.txt` when the resource is missing); src\Lines.cs line 188 (Tr asks for `lines-<id>-fr`, which isn't embedded)  
How to reproduce: Downloads\AdeeboMeter.exe with a file Downloads\knowledge\lines-chill-fr.txt (or %USERPROFILE%\knowledge\lines-chill-fr.txt, the `..` of Downloads). It contains `all.good|<any text, RLO characters, 110+ chars>`. Language = Français.  
What happens / what should happen: the widget and header speak that file's lines. Program.InDownloads exists so that files sitting next to a downloaded exe aren't trusted, and mine.txt gets Bidi cleaning and length limits (Lines.Clean), but these packs get neither. Use the disk fallback only in a developer build (no embedded resources at all), or never for a Downloads or portable-untrusted folder.  
Confidence: suspected (read it)

### 16. After an upgrade from 1.1.5, switching views brings back a bar without Sports
Severity: cosmetic  
Where: src\AppCtx.cs line 259 (the one-time Sports migration changes only Cfg.Nav), src\Views.cs lines 74 and 131-135 (Switch → Put sets Cfg.Nav from the view's stored 1.1.5 nav)  
How to reproduce: 1.1.5 settings with two saved views (e.g. My setup and Gamer, each with its own nav). Upgrade, then switch to the other view.  
What happens / what should happen: that view's 1.1.5 nav has no "sports" (and Toolbox in its old place), so the Sports page drops back into More. Apply the same one-time migration to every kept view's nav.  
Confidence: suspected (read it)

## Still present (from round 4)

| Round-4 # | Status in 999a100 |
|---|---|
| 16 (fresh install opens in Pro) | Fixed for the installer's autostart-only file (keys==0), but still present in another form: see #1 above |
| 6, 13, 18, 19, 20, 24, 31, 34, 78, 83, 108, 120 | Fixed (re-read: ARP alert matches both voices; Crash.Clean keeps names with spaces out; svchost close: 2; Report.Active guards History/Brain/LearnIsp; Known has mood/crlrev/buddy/hat/outagebest/ovat; Calls ignores loss when the check is off; ISP trace once per network with 2 retries; policy logs unknown values; Usage.Save on session end; Windows\Temp is "temp"; Wi-Fi service retried after 10 min; Mood clocks run in the sampler) |

## Checked and held up

- Language tables (fr, es, ar, bn, zh): every line parses (no lines without a tab, no duplicate keys, no empty translations), and there's no leading or trailing whitespace mismatch. Every `{0}`/`{1}` placeholder set matches English. No translation is an invalid .NET composite format where the English is valid, so no string.Format crash. There are no `\n` escape mismatches. es, ar and bn have identical key sets (5,380), and langcheck.py gives 100% of the L.T literals for each.
- Buddy packs: 29 voices × es/ar/bn all exist. Each has exactly the English key set and variant count, the same number of `{v}` and `{addr}` lines per key, no unknown placeholders, no unbalanced braces, and nothing over 110 characters.
- Settings upgrade from 1.1.5: every 1.1.5 key is read or ignored safely (`tbview`, `devalerts`, `reports` in nav dropped). The Sports and Toolbox migration runs once. "engineer" preset → Balanced. lang=fr kept. A saved zh opens in English.
- Policy: values only switch things off and are re-applied after a preset change, a view switch and an import. Keep() saves the user's own value. LearnIsp, Weather, Relay and the probes all respect it.
- `--report`: exit codes confirmed (1 for a warning verdict, 4 for a non-.json name; the JSON is valid). Report.Active keeps History, Brain and the ISP trace untouched.
- Points: one award per id, the replay needs 10 min on the steady clock and the boot tick, nothing is earned while off, NoEarn holds every gated tool, the total is capped at 1,200, and a damaged line is skipped. Hall of Fame: CleanName and MixedScript limit marks and refuse mixed scripts, and a long name shrinks rather than being cut.
- Monitor scheduling: the pace list is never empty (IfEvery is always > 0), "Off means off" for gw and net, the internet check obeys the policy, the TCP 443 fallback is only after ICMP misses, the join grace holds the first 45 s, and Wi-Fi state/channel words cover ar/es/fr/zh netsh.
- Setup: RemoveFolder refuses unless the app was just uninstalled. Install never "updates" backwards, keeps the user's autostart choice, and deletes only version-named folders.
