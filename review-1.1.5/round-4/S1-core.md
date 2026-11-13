# Round 4 - slice S1 (core): Program, AppCtx, Setup, Policy, Report, History, Model, Native, Points, HallOfFame, Mood, Lang, Examples, Qr, Monitor

Reviewed at e80412e. Every line of the 15 files was read, plus the code they call for the focus items: WlanApi.Current in src\Link\LinkInfo.cs, Alerts in src\Brain\Watch.cs, Brain.Second and Usage, Settings import, and .github\scripts\hof_verify.py.
Harnesses are in scratchpad/review4/h1 (hof.cs, pts.cs, mode.cs, misc.cs, pol.cs), scratchpad/review4/rep (--report run) and scratchpad/review4/hof (hof_verify run on a copy).

### 1. A fresh install, or "Just run it from here", always opens in Pro mode: the Simple start never shows
Severity: wrong result  
Where: src\AppCtx.cs line 210 (LoadSettings: `if (!first && Cfg.Mode == "") Cfg.Mode = "pro"`), with src\Setup.cs line 171 (Install: `SetLine(settings, "autostart", ...)` always creates the settings file) and line 454 (Press "here": the same for a copy run where it is)  
How to reproduce: On a PC that has never run AdeeboMeter, double-click the exe and press Install (or "Just run it from here"). The installed copy starts with an AdeeboMeter.settings that holds only `autostart=1`.  
What happens / what should happen: LoadSettings sees an existing file (`first` = false) with no `mode=` line, takes it for an upgrade from 1.1.4 and sets Mode = "pro". Every new user who goes through the installer gets Pro (every tab), not "shown as Simple with the choice on the home page" (Program.cs line 320). The `if (first) SaveSettings()` step is skipped as well. Should: treat a file with no `mode=` and no other 1.1.4 keys as a first start, or have Setup write `mode=` (empty) / a "fresh" marker.  
Confidence: confirmed (ran it: mode.cs calls AppCtx.LoadSettings on a settings file holding only `autostart=1` and gets Cfg.Mode = 'pro'; with no file it stays '')  

### 2. In Serious mode the ARP-spoofing pop-up never appears
Severity: security  
Where: src\Monitor.cs lines 550-551 (WatchGatewayMac: the serious text is "Gateway MAC flips ... possible ARP spoofing."), src\Brain\Watch.cs line 39 (Alerts.OnEvent raises the alert only when the text contains "hardware address")  
How to reproduce: Tray > Serious mode (no jokes). Have the gateway's MAC flip back within 10 minutes on the same network (an ARP spoofer, or `arp -s` games on a test LAN).  
What happens / what should happen: The timeline gets the level-2 event, but its serious wording has no "hardware address", so Alerts.OnEvent never raises "Your router's hardware address changed on the same network - possible spoofing". With the companion voice the same event does pop up. Should: match on the event kind and a fixed tag, not on the wording that Voice.P swaps.  
Confidence: confirmed (ran it: misc.cs feeds both texts to Alerts.OnEvent as kind "net", level 2. The companion text shows ALERT; the serious text shows NO ALERT)  

### 3. `--report` writes into the user's real history (and data files) and runs the ISP trace; the header says nothing is written
Severity: wrong result  
Where: src\Report.cs lines 17 and 28-31 (Monitor.Start inside the report); src\Monitor.cs line 91 (Events.Add "AdeeboMeter started") -> src\Model.cs line 117 (Timeline.Add -> History.Event); src\Monitor.cs line 151 (Brain.Second -> Flush: History.Minute, Usage.Add, Baseline.Minute, Devices.Check at :x0, AirLog at :05, UpdateBreak at :20); src\Monitor.cs line 266 (LearnIsp)  
How to reproduce: Copy r4.exe into an empty folder with AdeeboMeter.portable and run `AdeeboMeter.exe --report out.json --seconds 5`.  
What happens / what should happen: data\history\2026-11-13-events.csv appears with "AdeeboMeter started" and "On LAN (eth0), gateway ..." events. Each run that crosses a minute boundary also writes a minute row, a usage and baseline update, and possibly devices.txt, the air log or the Windows-update check. An RMM job that runs it every hour fills Time machine and the outage log with fake "started" and "On LAN" events. When the GUI copy is running at the same moment, the two processes append to the same history files, so rows are duplicated or lost to sharing errors. Each run also does the 6-hop trace and PTR lookup toward 1.1.1.1 that the JSON never uses. The Report header promises "Nothing is written except the file you name and the usual log". Should: run the monitor in a report mode with no Timeline-to-History link, no Brain.Second side effects and no LearnIsp.  
Confidence: confirmed (ran it under mono: the events file was created. The minute, usage and devices paths were read, not run)  

### 4. Import settings silently drops the voice (mood), certificate revocation, the buddy, its hat, the outage best score and the overlay position
Severity: wrong result  
Where: src\Program.cs line 370 (Cfg.Known has no "mood", "crlrev", "buddy", "hat", "outagebest" or "ovat"), used by src\Ui\FullView.Settings.cs line 544 (ImportSettings keeps Known keys only)  
How to reproduce: Settings > Voice: Sarcastic engineer. Turn Buddy off and switch certificate revocation checks on. Export settings, reset or move to another PC, then import the file.  
What happens / what should happen: The exported file has `mood=sarcastic`, `crlrev=1`, `buddy=0` and so on, but Import skips every key not in Known. The voice goes back to Companion, revocation checking goes back to its default, and the buddy and hat are lost, while the note says "Loaded N settings". Should: add the 1.1.5 keys to Known (or derive Known from Lines()).  
Confidence: confirmed (ran it: misc.cs lists the saved keys missing from Known: mood, crlrev, buddy, outagebest, hat, ovat)  

### 5. Hall of Fame: the social handle isn't covered by the claim code, so whoever submits a shared card first attaches their own profile to it
Severity: security  
Where: .github\scripts\hof_verify.py lines 125-141 (only name, points and date are checked, and the handle is taken from the form). It then blocks the real owner at lines 132-137 ("already"). Also src\HallOfFame.cs lines 23-25 (the comment says the code stops "someone else's card with a new name")  
How to reproduce: Take a card picture someone posted (the card is made to be shared, and the code is printed on it). Submit its name, points, date and code with "X" and your own handle before they do. Then submit the same card with the owner's GitHub handle.  
What happens / what should happen: The first submission is added with `"site": "x", "handle": "thief_handle"` and the page links the winner's name to the thief's profile. The real owner's submission gets "Ada Lovelace is already in the Hall of Fame." and cannot correct it. Should: tie the handle to the card (sign it in the card text), or accept a handle only from the issue author (`ISSUE_AUTHOR` for GitHub) and let a later matching submission from the same GitHub user change it.  
Confidence: confirmed (ran it: copied hof_verify.py to scratch with an empty hall-of-fame.json and HOF_KEY=testkey. The first run gave "added" with the thief's handle, the second gave "already")  

### 6. Mood: the provider "excuse" times an outage from the first outage of the day, and the header line never updates
Severity: cosmetic  
Where: src\Mood.cs lines 30-41 (downSince and greenSince change only inside Quip), src\AppCtx.cs lines 385-391 (a green widget is the folded chip, which never calls Quip), src\Ui\FullView.cs line 514 (the header repaints only on a level, headline, flow or hour change)  
How to reproduce: The view is closed and the widget shows the card during a red internet outage at 09:00 (downSince = 09:00). The line comes back, and the widget folds to the green chip, so Quip isn't called and downSince isn't reset. At 14:00 the provider goes down again.  
What happens / what should happen: The card at once says "<ISP>'s excuse, after 5 h: They've misplaced the internet...", counting from the morning outage. In the engineer view the excuse comes only when the headline changes, so it usually never appears, or keeps an old "after 3 min" for an hour. The koan's "two hours of all green" has the same problem: it is timed only from the moments the card or header happened to be drawn. Should: keep the down and green clocks from the Monitor (netDownSince / Judge), not from paint calls.  
Confidence: confirmed (ran it: misc.cs calls Mood.Quip for an outage, sets downSince 4 h back to stand in for the hours the folded chip skipped Quip, and the next outage's first quip says "after 4 h". AppCtx and FullView repaint rules were read)  

### 7. The JSON "finding" says "Nothing is wrong" while the verdict is "unknown"
Severity: wrong result  
Where: src\Report.cs line 95 (Json writes r.Finding unchanged after Verdict, at lines 79-84, has downgraded 0 to 3)  
How to reproduce: `--report out.json` on a PC where step 6 "The way in" couldn't run (a proxy-only network, or offline lab).  
What happens / what should happen: Output: `"verdict": "unknown", "exit_code": 3, "finding": "Nothing is wrong right now - your PC, the link, your router, the internet, names and the way in all work."`. A script or person reading the finding sees a healthy PC that the exit code calls unknown, and "the way in ... work" contradicts its own observed row. Should: when Verdict downgrades, replace or append to the finding ("one step couldn't run: ...").  
Confidence: confirmed (ran it: rep/out.json under mono, where step 6 couldn't resolve)  

### 8. Policy values other than off/no/false/0 are silently ignored, so IT believes a switch is off
Severity: wrong result  
Where: src\Policy.cs line 59 (Parse: `off = v == "off" || v == "no" || v == "false" || v == "0"`), with no log for an unrecognised value or key  
How to reproduce: AdeeboMeter.policy with `internet_check = disabled`, `internet_check = "off"`, `internet_check: off` or `speed_test = disable`.  
What happens / what should happen: None of these switch anything off, and nothing is logged. Policy.File is still set, so the `--report` JSON shows `"policy": "...AdeeboMeter.policy"` as if it applied. history_days already logs a bad value. Should: accept disable/disabled/n and quoted values, and log every line that sets nothing.  
Confidence: confirmed (ran it: pol.cs - only "off" and "OFF" set NoInternetCheck)  

### 9. Shutdown or logoff loses up to 5 minutes of data usage every time
Severity: wrong result  
Where: src\AppCtx.cs lines 290-295 (OnSessionEnding stops captures only), src\Brain\Brain.cs Usage.Add (saves at most every 5 min) - Usage.Save() runs only in Quit (AppCtx line 318)  
How to reproduce: Use the PC normally, then shut down Windows (no Exit from the tray). Compare Usage for today with what moved in the last minutes.  
What happens / what should happen: Windows ends the process after SessionEnding, so Quit and Usage.Save never run, and the last ≤5 minutes of the day's MB are lost. With a daily shutdown the monthly cap warning reads low. Should: call Usage.Save() (and flush the current history minute) in OnSessionEnding.  
Confidence: suspected (read it)  

### 10. The provider trace is repeated up to four times per network, and again on every switch back, while About says "once per network"
Severity: wrong result  
Where: src\Monitor.cs lines 508-533 (LearnIsp: `if (Isp == "" && ispTries++ < 3) ispKey = ""`; ispKey keeps only the last network), src\Ui\FullView.Settings.cs line 391 ("Once per network: a short trace (6 hops) toward 1.1.1.1 and one name lookup")  
How to reproduce: Use a network whose first public hop has no reverse-DNS name, or where ICMP time-exceeded is filtered (common on company networks). Or dock and undock between two networks.  
What happens / what should happen: Each failed learn re-runs the 6-hop trace and the PTR lookup at the next 10 s interface check, four traces in all. Going A -> B -> A runs the trace again for A. About under-states the background traffic. Should: remember the result per network key, failures too, for the session (or say "up to 4 times").  
Confidence: suspected (read it)  
Rule: 1  

### 11. Three transient Wi-Fi service errors switch the Wi-Fi watch to netsh.exe for the rest of the session
Severity: slow  
Where: src\Monitor.cs lines 463-469 and 483 (`++wlanApiFails >= 3` -> `wlanApiOk = false`, never set back)  
How to reproduce: Three Wi-Fi reads in a row fail on the Wi-Fi service. This can happen while the WLAN service restarts after a driver update or reset, or when WlanQueryInterface returns ERROR_INVALID_STATE (5023) during a reconnect.  
What happens / what should happen: After the third failure every later read starts netsh.exe (and a console host), every 30 s or every 10 s with the view open, until AdeeboMeter is restarted. That is the cost the new code was meant to remove. Should: retry the Wi-Fi service now and then (for example every 10 minutes, or after a network change or resume) instead of giving it up for the session.  
Confidence: suspected (read it)  

### 12. UI text and comments still describe netsh, Engineer and old figures
Severity: cosmetic  
Where: src\Ui\FullView.Settings.cs line 127 (Wi-Fi watch: "The priciest job here - it starts a small Windows tool (netsh) each time" - it now asks the Wi-Fi service); src\Ui\FullView.Settings.cs line 328 ("from Penny to Engineer" - Engineer was removed); src\Program.cs line 302 ("one netsh start every 30 s"); src\Program.cs line 11 (a portable copy's settings and log "in its data folder" - they are written next to the exe: rep/AdeeboMeter.log); src\Points.cs line 15 ("Off until you turn it on" - on from day 1) and line 28 ("about 580 in all" - 806 now); src\AppCtx.cs line 211 ("the game settings are kept" - ApplyPreset resets GameBgSec/GameBgServers); src\History.cs line 12 ("1-90 days" - up to 180); src\HallOfFame.cs line 141 ("32 characters fit at 18 px" - the loop's last size is 16 px)  
How to reproduce: Open Settings > Background checks > Wi-Fi watch, and Settings' "Tune it" tip.  
What happens / what should happen: The Wi-Fi row tells users the default job starts a program each time, so they may turn off a check that is now cheap. The other items are comments that no longer match the code.  
Confidence: confirmed (read it, and saw the log next to the exe in the portable run)  

## Still present from earlier rounds

| earlier id | where now | evidence |
|---|---|---|
| R1#43 / R2#58 (version 1.1.4) | src\Program.cs lines 115-116 | `Version = "1.1.4"`, and the report JSON says "AdeeboMeter 1.1.4". New effect: on a PC with 1.1.4 installed, SetupWindow.Same is true (Setup.cs line 321), so the 1.1.5 download offers "Open AdeeboMeter" and starts the OLD copy. Single.RunningIsOlder (Program.cs line 463) never replaces a running 1.1.4 |
| R2#4 (Key and Code public, card signable with the exe) | src\HallOfFame.cs lines 32, 59 | Unchanged. The comment at lines 23-25 accepts the limit |
| R2#74 (Points "off by default" text) | src\Points.cs line 15 | "Off until you turn it on (Settings)", while Cfg.Points = true |
| R2#101 (BuildSnap every second) | src\Monitor.cs line 149 | Builds a full Snap with Details every second whether shown or not |
| R2#103 (interfaces, ARP and DriveInfo every 10 s) | src\Monitor.cs lines 266, 409 | IfEvery 10 s (60 s on Penny only) |
| R2#106 (abandoned PTR task) | src\Monitor.cs lines 523-524 | `t.Wait(3000)` then the task is left running |
| R1#152 (wall time in Ago) | src\Model.cs lines 125-128 | Dedupe now uses Clock.Mono, but Ago (used for "Internet back after ...") still subtracts DateTime.Now |

## Checked and held up

- WlanApi.Current: the WLAN_INTERFACE_INFO stride (532) and isState (+528), and the WLAN_CONNECTION_ATTRIBUTES offsets (SSID 520/524, BSSID 560 after the 2-byte pad, quality 576, rx 580, size ≥ 588), contain only DWORD, GUID and byte fields. They are the same on x86, x64 and ARM64. WlanFreeMemory runs on every path that owns memory (list, both queries, early returns inside try). The handle is closed in finally. No WlanRegisterNotification is used, so no callbacks run on other threads. A stopped service (OpenHandle fails) or no adapter (n ≤ 0) returns -1, and that read falls back to netsh. Location off returns 5, which sets the same "blocked" state as netsh. The units match netsh: kbps/1000 = Mb/s, signal quality %, channel opcode 8.
- Wi-Fi: lowercase colon BSSID and the "disconnected" / "connected" state strings match NormState, so drop, roam and channel events work with either source.
- Presets: a saved `preset=engineer` reads as Balanced and gets Balanced's timings (ApplyPreset after LoadSettings). Balanced is Wi-Fi 30 s with the view closed and 10 s open, and Gamer is 10 s. JobsForPreset and GameJobsForPreset match ApplyPreset. A custom preset keeps its own timings.
- Points: everything earnable is 806 (pts.exe over PtAll: 359 items = 791, plus 5 replays x 3), under Max 900 = hof_verify MAX_POINTS. AwardAt is 500 in both. Retired "finddev" still counts. The replay rule uses the boot ID and the uptime check.
- CleanName vs clean_name: 349 random names from every Unicode plane round-trip. The claim code Python computes from the name printed on the card equals the C# code. The only differences were mono's old normalization and category tables, not .NET Framework. No newline can reach GITHUB_OUTPUT, because all whitespace collapses to " ". Handle regexes keep each link on its own site.
- Report: JSON escaping (Q) handles quotes, backslashes and control characters. Level is always an integer string (GuideRun.Obs). Exit 4 for no file name, a bad path or a non-.json name. Exit 3 for any exception. A short --seconds is clamped to 5-120.
- Policy: at most 64 K characters and 2,000 lines are read (no OOM on a huge or one-line file). Both files are read and the strictest value wins. Every key only narrows. mode accepts simple only. history_days is clamped to 1-180.
- Native: DNS_RECORD offsets (2P / 2P+16), the job-object structs, MEMORYSTATUSEX, SYSTEM_POWER_STATUS and token elevation (class 18, TOKEN_QUERY) match the Windows headers on 32- and 64-bit. TCP/UDP owner-PID row sizes (24/56, 12/28) are right.
- Mood is text only: one line of voice after the verdict, the grade wording and season lines. It changes no verdict, alert, probe or pace. Its cost is a few small arrays per card draw. Serious hides all of it.
- Portable detection: only the Downloads and Desktop folders themselves are distrusted (a folder unzipped inside them is honoured). The result is cached. A read-only portable copy falls back to AppData.
- QR: the block table for versions 1-10 at level L, the version information and the finder penalty with the quiet zone match ISO/IEC 18004.
- Startup and shutdown: SystemEvents are subscribed on the STA UI thread, so their events arrive there. Quit unhooks every static event, stops the monitor and game threads and runs CleanCaptures. The single-instance listener now waits 30 s between checks.
