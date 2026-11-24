# P4: Watch, Link, Capture, WindowsDeep (1.1.6 at 999a100)

What src/Watch is: the 1.1.6 "watch a far end" modes, plus the Markets and Sports pages.
- **Streaming watch** (Streams.cs): finds a stream from the DNS cache and the connection table, knocks on the stream's server every 10 s, and judges each dip (WatchKit.cs, 90-day watch-YYYYMM.log).
- **Broker watch** (Trading.cs): a TCP knock on the user's broker while NYSE/TSX are open. Markets.cs works out the holiday calendar on the PC.
- **Line Check** (LineCheck.cs), **Weekly** report and **LineHours** (from History).
- **Markets data** (MarketData.cs): Bank of Canada Valet (FX since 2017, BCPI groups, policy rate and CPI), exchange hours, and index and futures quotes from financialmodelingprep.com with the user's own key.
- **Sports** (Sports.cs): NHL api-web.nhle.com, ESPN site.api.espn.com (6 leagues), football-data.org and api.cricapi.com with the user's own keys, and BBC Sport RSS (3 feeds).
- **Stream Check** (StreamCheck.cs): fetches the user's HLS/DASH link and downloads up to 30 s of video.

All of it uses HTTPS. The keys are stored with DPAPI and kept out of the log. There are no API keys in the source.

Harnesses are in `.../scratchpad/review5/P4-watch-link-deep/h/` (A.cs: MarketData.Stale and culture clocks; B.cs: HwTools.Seller). Both run with mono against r6.dll.

### 1. Sports, index/futures quotes, the Markets history downloads and Stream Check are missing from About › "What leaves your PC" and from docs/privacy.html. The kick-off reminder fetches every 30 minutes with the window closed
Severity: security  
Where: src\Watch\Sports.cs line 125-128 (ReminderTick → Fetch), lines 159-170 (Fetch: NHL, ESPN, football-data.org, cricapi.com, 3 x feeds.bbci.co.uk); src\Watch\MarketData.cs line 332 (FetchQuotes → financialmodelingprep.com), lines 115-121 and 162-186 (Fetch: one bankofcanada.ca request per currency since 2017, plus BCPI and CANADA); src\Watch\StreamCheck.cs line 128; the list itself is src\Ui\FullView.Settings.cs lines 497-520 and docs/privacy.html lines 75-108  
How to reproduce: Sports › Turn on (or press "Follow teams...", which turns Sports on without the consent text, FullView.SportsPage.cs line 149). Follow "TOR" and turn on "Remind me in the corner 15 minutes before kick-off". Close the window to the corner and watch with a proxy log. Then open About › "What leaves your PC".  
What happens / what should happen: With the window closed, AppCtx.cs line 507 calls Sports.ReminderTick about once a minute. Every 30 minutes it runs the full Sports.Fetch: the NHL, ESPN (once a day), football-data.org or cricapi.com when a key is set, and all three BBC feeds. That is a background request series to 4-6 third-party hosts. It isn't in About's list or on the privacy page, so About's "Every packet it sends is listed" isn't true. The Markets page sends 3 to 15 Valet requests per visit (each FX series back to 2017) plus FMP quotes. About says "one request for the day's exchange rates and, about weekly, one for the commodity index". About does not list financialmodelingprep.com, the sports hosts or Stream Check at all. Add Sports (with the reminder's 30-minute background pass), Markets (Valet history, plus FMP with your key) and Stream Check to the About rows and to privacy.html. Have "Follow teams" ask before it turns Sports on.  
Confidence: suspected (read: the call path and the About/privacy text; no packet log here)  
Rule: 1

### 2. The background kick-off pass spends the user's CricketData quota and reloads BBC headlines that nobody sees
Severity: slow  
Where: src\Watch\Sports.cs line 128 (ReminderTick → Fetch(null)), lines 162-164 and 271-272 (Cricket: up to 2 calls per pass)  
How to reproduce: Add a CricketData key, follow a team, turn the reminder on, and leave the app in the tray for a day.  
What happens / what should happen: Each 30-minute pass refetches everything: up to 2 CricketData calls (page 1 and page 2), 3 RSS feeds and the NHL. 48 passes a day reach the 90-call stop by mid-afternoon. When the user then opens Sports, they see "Today's 90 calls are used". The reminder only needs the fixture lists of followed teams. It should skip the RSS feeds and cricket page 2, and fetch only when a followed game could start within the next hour.  
Confidence: suspected (read; the arithmetic is 48 x up to 2 = 96 > 90)  
Rule: 7

### 3. Markets › Currencies counts exchange rates up to 3 business days old as "fresh", so the chart and the converter show old rates without saying so
Severity: wrong result  
Where: src\Watch\MarketData.cs line 145 (Stale: `s.LastDay < want.AddDays(-3)`), used by Fetch line 165; Cross() line 226 (the converter)  
How to reproduce: Harness A.cs loads a Valet answer whose last day is N days back, then calls Stale. On Tue 24 Nov 09:32 ET, data ending Mon 23, Fri 20 or Thu 19... gives: Tue → false, Mon → false, **Fri 20 → false**, Thu 19 → true. After 16:45 the same rule accepts Friday's rate on a Monday evening and Monday's on Thursday.  
What happens / what should happen: The per-currency history (FX:xxx) behind the Currencies chart, the "since" changes and the converter (Cross) is refreshed only when it is more than 3 days behind the last business day. A cached series from Monday is therefore shown on Thursday as the current rate. Trading.Rates (FxStale) uses the correct rule, so two parts of the Markets page can show different "latest" USD/CAD values at the same time. The tolerance should be "older than the last business day". A Bank holiday then costs at most one extra fetch per 30 minutes, which the `tried` clock already limits.  
Confidence: confirmed (ran it: A.cs)

### 4. HwSpecs.Get keeps its lock through every WMI query, and WMI's connect step has no timeout. A stuck WMI service hangs every later hardware caller, including "My PC is slow"'s 40-second deadline
Severity: slow  
Where: src\WindowsDeep\HwSpecs.cs line 34 (Get: `lock (k) { ... last = Read(); }`), line 43 (Wmi: `new ManagementScope(scope)`, with no ConnectionOptions timeout. EnumerationOptions.Timeout only covers the step from one result to the next); src\WindowsDeep\PcHealth.cs line 294 (same pattern); callers: src\Checks\Investigate.cs line 319 (HwTools.Quick with Deadline +40 s), src\Checks\IspReport.cs line 183, Investigate.Full.cs line 70  
How to reproduce: Put a PC's WMI repository into the classic "winmgmt not answering" state (`net stop winmgmt` and then suspend the process, or a broken repository). Then run Toolbox › "Why is this PC struggling?", then Report for my ISP.  
What happens / what should happen: ManagementObjectSearcher.Get() connects with the default ConnectionOptions, whose timeout is unlimited. The first caller blocks inside `lock (k)`. Every later HwSpecs.Get() (ISP report, Full checkup, Hardware tools) queues behind it forever. Stop does nothing because none of these steps checks run.Stop, and the 40-second Deadline isn't passed down. Fix: set `new ConnectionOptions { Timeout = 8 s }`, read outside the lock (or with Monitor.TryEnter), and let callers that have a deadline use the cached `last`.  
Confidence: suspected (read; WMI not available here)

### 5. An unreadable sporthits.txt crashes the app from the sports thread
Severity: crash  
Where: src\Watch\Sports.cs line 116 (HitsToday catches only IOException), called at line 163 inside `try { ... } finally { ... }`, which has no catch  
How to reproduce: Create `data\sporthits.txt` as a folder, or deny the user read access to the file. Add a cricket key and open Sports.  
What happens / what should happen: File.ReadAllText throws UnauthorizedAccessException. Line 163 sits outside the per-sport `sport()` catch, so the exception escapes a plain background Thread and .NET ends the process. Catch UnauthorizedAccessException as Hit() already does, or wrap the cricket branch in `sport()`.  
Confidence: suspected (read)

### 6. Wi-Fi 6E: a 6 GHz channel numbered 1-14 is read as 2.4 GHz, so Stream Check tells the user to "Join your router's 5 GHz Wi-Fi"
Severity: wrong result  
Where: src\Watch\StreamCheck.cs line 317 (`fiveG = chn > 14`), lines 319-320 (the hop note "· 2.4 GHz"), line 344 (the tip); src\Link\LinkInfo.cs line 577 (BandOf: channels 32-177 become "5 GHz" when netsh gives no Band and the BSS list doesn't hold this BSSID)  
How to reproduce: Join a 6 GHz network on channel 5 (a common 6E default). Run Sports › Test my stream on a link that stutters.  
What happens / what should happen: m.WifiChannel comes from WlanApi.Current (line 234, channel number only, no band). The way along reads "signal fair · 2.4 GHz", and the first tip is "Join your router's 5 GHz Wi-Fi, or plug in a cable" for someone already on the fastest band. Use the band the Monitor or the BSS list already knows (WlanApi.List gives Band from the frequency).  
Confidence: suspected (read)

### 7. Times written with "h tt" lose AM/PM on Windows formats that have no AM/PM designator: "8-10 runs about 2x slower" with no way to tell morning from evening
Severity: cosmetic  
Where: src\Watch\LineHours.cs line 307 (Clock: `"h tt"`), used by SlowWords line 305 (Outlook › "Your line by the hour"); src\Watch\LineCheck.cs line 239 (`"ddd d, h:mm tt"`); src\Ui\FullView.SportsPage.cs lines 154 and 243 (`"ddd h:mm tt"`, `"dddd h tt"`, the kick-off time)  
How to reproduce: Set the Windows format to French (France) or German (on .NET Framework these have empty AMDesignator/PMDesignator; on mono A.cs shows the same empty value for ar-SA: `Clock(8)='8 '`, `Clock(20)='8 '`). Have 5 days of History with a slow evening.  
What happens / what should happen: The line reads "8-10 runs about 2x slower than the quietest hours". The Sports lead's "Kick-off (your time): sam. 7:30" can be 7:30 or 19:30. When the culture has no AM/PM designator, use `"t"` or `"HH:mm"`.  
Confidence: suspected (ran the culture probe on mono, which shows the empty designator for ar-SA; fr-FR and de-DE behaviour is from .NET Framework/Windows NLS data)

### 8. The monthly provider report gets a Hijri heading when the Windows format is Arabic (Saudi Arabia)
Severity: cosmetic  
Where: src\Watch\WatchKit.cs line 218 (`month.ToString("MMMM yyyy", CultureInfo.CurrentCulture)`); src\Watch\Weekly.cs line 116 (`"ddd d MMM"`)  
How to reproduce: Windows format ar-SA (default calendar Um Al-Qura). Copy the Streaming or Trading month report for November 2026.  
What happens / what should happen: The report covers Gregorian November (the filter on line 214 uses Year and Month), but its heading prints the Hijri month and "1448" (A.cs: `cal=UmAlQuraCalendar`). Anyone reading the heading (a provider, or the user) sees a month that doesn't match the dated lines below it, which use InvariantCulture. Format with a Gregorian calendar (or InvariantCulture plus a month name) in text meant to be sent on.  
Confidence: confirmed (ran it: the calendar and month output on mono)

### 9. Used-laptop seller check: a decimal comma ("1,5 TB SSD") becomes "5 TB" and is flagged red
Severity: wrong result  
Where: src\WindowsDeep\HwTools.cs line 463 (disk regex `\d(?:\.\d)?\s*TB`), lines 469-474  
How to reproduce: Harness B.cs: a PC with a 1,500 GB NVMe disk, seller text "i7, 16GB RAM, 1,5 TB SSD".  
What happens / what should happen: The row reads `2 | Seller said: 5 TB SSD | this PC's disk is 1,500 GB`, and the Verdict lists it as something to haggle over. "1.5 TB SSD" matches. Accept `[.,]` as the decimal mark (the app ships fr, es and bn, and German or French listings write "1,5 To/TB").  
Confidence: confirmed (ran it: B.cs)

### 10. The broker watch runs a DNS lookup on every round while the broker's name doesn't resolve or points to a private address
Severity: slow  
Where: src\Watch\Trading.cs lines 83-91 (Step: `if (hostIp == null || ...)` re-resolves, and line 91 sets hostIp = null after the private-address refusal)  
How to reproduce: Type a broker name with a typo, or one that a filtering DNS sends to 0.0.0.0 or 192.168.x.x, with Live watch on, during market hours.  
What happens / what should happen: The L-57 "asked once" cache only holds a success. After a failure (or a refusal) every Step resolves again: every 10 s on Balanced and every 2 s with the engineer view open, for the whole 6.5-hour session. That is about 2,300 DNS queries a day, none of them listed in About. Keep the failure for the same 10 minutes (resolvedAt is already set).  
Confidence: suspected (read)

### 11. [day 1] Markets › Line Check and Trading show "Starting..." outside market hours when the broker watch has no samples
Severity: cosmetic  
Where: src\Watch\Trading.cs line 139 (Headline: no samples → `Status != "" ? Status : "Starting..."`)  
How to reproduce: Fresh install, pick a broker on a Saturday (or after 16:00 ET). Open Trading.  
What happens / what should happen: Headline returns "Starting..." although nothing will start until Monday 09:30 ET. Close() clears Samples at the close, so the evening after every market day shows the same thing. FullView.Trade.cs lines 63/119 compute `openNow` themselves. Headline should say "Market closed - the watch starts at 9:30 ET" when `!Watching`.  
Confidence: suspected (read; the page code was only partly traced)

## Still present (from round 4)

| Round-4 # | What | Now |
|---|---|---|
| 37 (b) | Open/WEP network not flagged on non-English Windows | Same English-only test in a new place: src\Link\LinkInfo.cs line 670 compares `F("Authentication") == "Open"`, so "Ouvert" / "Offen" / "Abierta" show the open hotspot's Security row green ("0"). The WlanApi BSS list (`Sec = "Open"`, language-free) is already read at line 678. Part (a), the location words, is fixed: LocWord/PermWord are localized and m.WifiBlocked is used. |
| 132 | Tools contact services without a consent step | Same pattern in 1.1.6: "Follow teams..." (SportsPage line 149) turns Sports on with no consent text (see #1). |

## Checked and held up
- Markets.cs holiday rules for 2026-2028: US observed rules (New Year's on a Saturday not moved; Juneteenth/July 4 on a Saturday → Friday), the day after Thanksgiving and Dec 24 early closes, TSX Victoria Day and Christmas/Boxing pairs, Easter computus. All correct.
- Trading.EasternNow and UsEastern fallback DST (2 am second Sunday of March to first Sunday of November); exchange zones; NextSession Kind handling.
- JSON/feeds: Json.Parse depth cap 64 and 100k array cap; HttpSmall size cap while reading, a 20 s total deadline, same-site redirects only with no https→http; RSS Elems is single-pass with CDATA; ESPN cache parsed before it is kept; NHL/football/cricket schema changes raise "changed feed", not "no games"; FMP error text never shown (the key can't leak); keys stored with DPAPI and not exported; ParseFx/ParseValet/ParseIdx reject NaN and out-of-range values.
- StreamCheck: the token is never logged (host only), links are DPAPI-encrypted, redirects are followed by hand with private-address checks at connect, 2 MB playlist cap, 300 MB / 30 s download cap, a 404 on one quality falls back to the next.
- WatchKit store: under its own file lock, reads on a background thread with a 4 x 5,000-line cap, clock-back handling in DipMaker.
- Streams/Trading: locks, Copy() for readers, End() writing outside lock k, a stall counted as a dip, the per-minute point merge past 600.
- Link: NormBand "2,4 GHz" / "2,4 ГГц" (round-4 #36 fixed); localized rate, location and user-disconnect words (#38 fixed); WlanApi buffer bounds and offsets; HT/VHT/HE/EHT width parsing incl. 320 MHz and WPA3-Enterprise/FILS (#125 fixed); LinkInfo.Collect and AirLog run off the UI thread.
- Capture: Start/Stop serialised on `life`; generation-guarded restart; a reader ending by itself doesn't kill the replacement; tshark's dumpcap child is stopped; the ring folder is emptied; the time-limit timer also fires with no packets; live_capture=off says so (#72 fixed). Journey/Packets: filters whitelisted, snaplen 128/512, ICMP Payload never null.
- WindowsDeep: disk test file delete-on-close; Stop checked in every phase; memory test survives OOM; CPU test gates the timer; PcRecord uses a monotonic clock; DevInfo CR_BUFFER_SMALL retry; the Startup ExeOf quote handling doesn't throw.
