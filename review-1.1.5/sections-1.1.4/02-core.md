# AdeeboMeter v1.1.4 - adversarial review, section 02: core, startup, persistence, monitor loop, brain

Scope: src/Program.cs, AppCtx.cs, Setup.cs, Model.cs, History.cs, Monitor.cs, Brain/Brain.cs, Brain/Watch.cs, Native.cs,
SelfTest.cs, release.ps1, build_exe.cmd, .github/workflows/build.yml. Static read at commit 0965fa1 (Windows-only, not run).

Severity key: Critical = data loss / security / crash for most users; High = wrong behaviour most users will hit or a broken
promise; Medium = real users will hit under common conditions; Low = edge case / polish.

No Critical findings. The P/Invoke struct layouts (TCP/UDP owner-PID tables v4+v6, DNS_RECORD, DNS cache entry, job object,
MEMORYSTATUSEX) were checked offset by offset and are correct. Background loops are wrapped in try/catch, so I found no path
where a background exception ends the process.

---------------------------------------------------------------------------------------------------------------------------

## A. Confirmed (by reading code)

### H1 (High) - "Just run it from here" turns on Start with Windows even when the user switched it off, takes over the installed copy's autostart, and the entry outlives Uninstall
- src/AppCtx.cs:40, 62, 189-205, 218-228; src/Setup.cs:410-414 (SetupWindow.Press "here")
```csharp
bool ... autoStart = true ...                                   // AppCtx.cs:40
if (first) { ApplyAutoStart(); SaveSettings(); }                 // AppCtx.cs:205 - first run = no settings file next to the exe
if (autoStart) k.SetValue("AdeeboMeter", "\"" + Application.ExecutablePath + "\"");   // :226
...
if (key == "here" && st == St.Offer) { ... startHere = true; Close(); return; }       // Setup.cs:413 - the switch state is dropped
```
- Scenario: the user downloads the exe, turns the offer card's "Start with Windows" switch off, and clicks "Just run it from
  here". SetupWindow.autoStart is thrown away. AppCtx starts with no settings file, so autoStart stays true and the Run key
  points at `Downloads\AdeeboMeter.exe`. Variants:
  - The user opened it from the zip preview. Explorer's temp copy (`%TEMP%\Temp1_...zip\...`) is what gets registered, and
    after a temp cleanup that Run entry is dead.
  - The user already has the installed copy (not running) and runs a downloaded copy "here". The Run value is overwritten,
    so the installed copy stops autostarting. If they later uninstall from Settings > Apps, `Uninstall` keeps the Run value
    because it isn't `Under(Base)` (Setup.cs:186), and the Downloads copy keeps starting at every login after the app was
    "removed".
  - Line 62 calls `ApplyAutoStart()` on every start, so whichever copy ran last owns autostart.
- Fix: pass the switch state to AppCtx for "here" (e.g. write `autostart=0/1` with `Setup.SetLine` before closing). Default
  autostart to off for copies that are not `RunningInstalled`. Never overwrite a Run value that points at an installed copy
  under `Base` from a non-installed copy. Uninstall should remove any Run value whose target is an `AdeeboMeter*.exe`.

### H2 (High) - `GwEverReplied` is never reset: one network whose gateway ignores ping gives a permanent red "your local network is dragging"
- src/Monitor.cs:47, 217, 242, 665-667, 676, 684
```csharp
GwPing.Add(g); if (!double.IsNaN(g)) GwEverReplied = true;        // :217 - only ever set
if (netChanged) { ... NetPing.Clear(); Loss.Clear(); NetEverReplied = false; ... }   // :242 - GwPing / GwEverReplied untouched
...
else if (!hasWifi) link = gwEver ? 2 : 3;                         // :667
if (link == 2) { isp = 3; dn = 3; inet = 3; }                     // :676
```
- Scenario: a laptop is used at home (the router answers ping, so `GwEverReplied=true`), then docks at an office or hotel whose
  gateway drops ICMP (common on enterprise firewalls, some ISP CPEs, cloud and hotel networks). After 5 NaNs, `GwPing.Avg(5)`
  is NaN, `link=2`, and the headline reads "Found it: your local network is dragging (gateway - ms). The PC is innocent."
  ISP, DNS and Internet are greyed out even though the internet works. This lasts until the app restarts.
- Wi-Fi users hit it too whenever `HasWifi` is false (see M2: netsh location block or a non-English Windows).
- Airplane mode, a pulled cable, or Wi-Fi off all lead to the same code: `Gateway==""`, `PingMs("")` returns NaN, and the
  verdict says the network is "dragging" instead of "you're not connected".
- Fix: on `netChanged` (and when `Primary.Id` or `Gateway` changes) clear `GwPing` and set `GwEverReplied=false`.
  Distinguish "no gateway / adapter down" (offline verdict) from "gateway silent but internet answers" (gateway just ignores
  ping - neutral).

### H3 (High) - False "possible ARP spoofing" alarm (red + pop-up) when moving between networks that share a gateway IP
- src/Monitor.cs:405, 432-445, 243 (10-second cadence); src/Brain/Watch.cs:38 (pop-up)
```csharp
string gw, key; lock (L) { gw = Gateway; key = IfaceId + "|" + Gateway + "|" + Ssid; }
if (gw == "") { gwMacKey = ""; gwMac = ""; return; }
if (key != gwMacKey) { gwMacKey = key; gwMac = mac; return; }
if (mac != gwMac) { Events.Add(2, "net", "...someone may be pretending to be it (ARP spoofing). Use the VPN until you know." ...
```
- Scenario: the key is "adapter + gateway IP + SSID". `192.168.0.1` and `192.168.1.1` are on most home routers, and SSIDs
  repeat a lot (eduroam, chain coffee shops, ISP default names, two "NETGEAR" homes).
  - The user closes the lid at home and opens it at a friend's place or a café with the same SSID and gateway IP. No tick ran
    while the gateway was "" (it only runs every 10 s and never during sleep), so the key is unchanged and the MAC differs.
    Result: a level-2 timeline event "someone may be pretending to be your router" and a Windows pop-up.
  - With netsh blocked by the Win11 24H2 location permission (or a non-English netsh, see M2), `Ssid` is always "". Then any
    two networks with the same gateway IP on the same adapter raise the alarm.
  - A replaced router, a failed-over clustered firewall or a mesh system raises this level-2 alarm, and `RoutingTools.GwTick`
    also posts a contradicting level-1 "router failover" event for the same change.
- Fix: reset `gwMacKey` on `netChanged`, on resume, and on any IP address or DHCP lease change. Include the DHCP server ID or
  Brain.NetKey history in the key. Raise level 2 only when the MAC flips back and forth (A to B to A) within minutes, or
  when two IPs share one MAC. Otherwise post one level-1 "router hardware changed" event. Make the two detectors agree.

### H4 (High) - All monitor scheduling uses the wall clock: setting the clock back freezes probes (and can freeze the sampler)
- src/Monitor.cs:201-253 (probe), 127-141 (sampler), 674 (`netStale`); AppCtx.cs:300, 306 (`lastTop`, `lastCover`)
```csharp
if (gwSec > 0 && (t0 - lastGw).TotalSeconds >= gwSec - 0.2)        // DateTime.UtcNow
...
int wait = Math.Max(2, pace) * 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;   // probe
int wait = 1000 - (int)(DateTime.UtcNow - t0).TotalMilliseconds;   if (wait > 0) stopping.WaitOne(wait);   // sampler
```
- Scenario: dual-boot Linux/Windows (RTC in UTC) or a fast CMOS clock. AdeeboMeter autostarts, then w32time corrects the clock
  back by N hours.
  - Every `(t0 - lastX)` is now negative, so no gateway pings, internet pings, Wi-Fi reads, connection or process reads happen
    for N hours.
  - `netStale` is negative, not stale, so old pings are still judged as "now".
  - `lastCover` is in the future, so the widget stops folding when windows cover it.
  - If the jump lands inside a tick (likely, since a probe tick runs netsh with a 3 s timeout), `wait` becomes
    `pace + N hours` and the thread sleeps for hours. The sampler thread is affected the same way, so the widget freezes.
  - UTC jumps also happen on manual time changes and on NTP corrections after a long sleep.
- Fix: use a monotonic clock for all intervals (`Stopwatch` or `Native.GetTickCount64`, which is already imported). Clamp
  `wait` to `[0, pace*1000]`.

### M1 (Medium) - "Router ping: Off" is ignored while the engineer view is open (breaks "you can switch each one off")
- src/Monitor.cs:209
```csharp
int gwSec = ViewOpen ? 2 : Cfg.GwSec, ... wifiSec = ViewOpen ? 10 : Cfg.WifiSec, appsSec = ViewOpen ? 2 : Cfg.AppsSec ...
```
- Scenario: the user sets Router ping to Off in Settings, and About says "Off.". Opening the engineer view still pings the
  gateway every 2 s. `netSec` handles this correctly (`Cfg.NetSec == 0 ? 0 : ...`); `gwSec` does not.
- Fix: `int gwSec = Cfg.GwSec == 0 ? 0 : ViewOpen ? 2 : Cfg.GwSec;` (wifi/apps are local-only, but apply the same idea for
  consistency).

### M2 (Medium) - Wi-Fi state is parsed from English netsh labels, and the netsh location block isn't detected in the monitor
- src/Monitor.cs:380-402
```csharp
if (k == "Signal") ... else if (k == "SSID") ssid = v; else if (k == "Channel") ch = v;
else if (k == "State") state = v.ToLowerInvariant(); else if (k.StartsWith("Receive rate", ...)) ...
lock (L) { HasWifi = sig >= 0; ... }
```
- Scenario 1: on German Windows ("Status", "Kanal", "Empfangsrate"), `state` and `ch` are always empty, so "Wi-Fi dropped",
  "reconnected" and "channel changed" never fire, and Calls/Alerts lose the drop signal.
- Scenario 2: on Spanish ("Señal") or other localized Windows, `sig=-1`, so `HasWifi=false`. A Wi-Fi laptop is then labelled
  "LAN", signal never enters the verdict, and H2/H3 get worse.
- Scenario 3: Win11 24H2 with location off makes netsh print "Network shell commands need location permission...". The same
  thing happens: `HasWifi=false`, `Ssid=""`. `LinkInfo.Blocked()` already detects this message (it is tested in
  SelfTest.cs:408) but ProbeWifi doesn't use it.
- Fix: read Wi-Fi state from the native WLAN API (`WlanApi` already exists in the codebase), or at least key off
  `AdapterKind=="Wi-Fi"` for `HasWifi`. Detect `LinkInfo.Blocked(outp)` and surface "Wi-Fi details need Location permission"
  instead of treating it as LAN.

### M3 (Medium) - Closing the setup window during Working (Alt+F4 / taskbar "Close window") leaves a half install or uninstall
- src/Setup.cs:273-278, 425-450; src/Program.cs:106-107
```csharp
if (e.KeyCode == Keys.Escape && st != St.Working) Close();          // only Esc is blocked
Task.Factory.StartNew(() => { ... Setup.Install(...) / Setup.Uninstall(...) ... });   // background thread
using (var w = new SetupWindow(uninstall)) { Application.Run(w); here = w.RunHere; }  if (!here) return 0;   // process exits
```
- Scenario: during the ~2 s install (StopOthers wait, copy, registry, old-version cleanup with 220 ms sleeps) the user presses
  Alt+F4. The window is borderless but still gets SC_CLOSE, and ShowInTaskbar=true adds the taskbar close. `Main` returns and
  the thread-pool thread dies mid-operation. Possible end states:
  - the exe is copied but there is no Uninstall key or shortcut;
  - old versions are deleted but the new one has no Uninstall entry;
  - on uninstall, the running app is killed but nothing is removed.
- Fix: override `OnFormClosing` and cancel while `st == St.Working`. Or run the work on a foreground thread and `Join` it
  before `Main` returns.

### M4 (Medium) - A read-only exe folder (Program Files, a network share, a locked-down PC) shows the install offer on every launch, including the login autostart, and never saves settings
- src/Setup.cs:52-56; src/Program.cs:103; src/AppCtx.cs:189-216
```csharp
return !File.Exists(Path.Combine(Program.Dir, "AdeeboMeter.settings")) && ...;   // ShouldOffer
```
- Scenario: IT copies the exe to `C:\Program Files\AdeeboMeter\` (or it runs from `\\server\tools`). The user picks "Just run
  it from here". `SaveSettings` fails silently (only logged, and the log also fails). The next launch with no args shows the
  card again, and so does the Run-key start at every login. All Settings changes are lost at exit. `Program.Log` also writes
  only next to the exe, so there are no diagnostics either.
- Fix: probe that `Program.Dir` is writable. If not, keep settings and log under `%LOCALAPPDATA%\AdeeboMeter` (DataDir),
  remember "offer declined" there, and never offer when started from the Run key (add a `--autostart` arg to the Run value).

### M5 (Medium) - Downgrade is labelled "Update" and deletes the newer installed version
- src/Setup.cs:280-281, 341, 344, 169-172
```csharp
bool Upgrade { get { return instVer != null && instVer != Program.Version; } }
string main = Same ? "Open AdeeboMeter" : Upgrade ? "Update to " + Program.Version : "Install";
```
- Scenario: the user has 1.2.0 installed and double-clicks an old 1.1.4 exe left in Downloads. The card says "Update to 1.1.4 -
  You have 1.2.0. Your settings come along." Install then deletes `...\1.2.0\` ("Tidying up older versions"). Settings
  written by 1.2.0 may lose keys that 1.1.4 doesn't know.
- Fix: compare with `System.Version`. If older, show "You have a newer version (1.2.0) - Open it" and offer the downgrade only
  as an explicit secondary action.

### M6 (Medium) - A "run from here" copy can't be updated by running a newer download; renamed copies escape StopOthers / KillOthers
- src/Setup.cs:52-56, 87-100; src/Program.cs:110-120, 275-282
- Scenario: the user once chose "Just run it from here" in Downloads, so `Downloads\AdeeboMeter.settings` exists. A browser
  saves the next release as `Downloads\AdeeboMeter (1).exe`. Double-click: `ShouldOffer()` is false (the settings file
  exists), so there is no update card. The mutex is held by the old copy, so `AskRunningToOpen()` opens the old copy's
  engineer view and the new version exits. The user believes they updated.
- Separately, `GetProcessesByName("AdeeboMeter")` doesn't match a process named `AdeeboMeter (1)`. So "close the stuck one"
  (KillOthers) and Install's StopOthers can't kill a hung or old renamed copy. The mutex `WaitOne(5000)` then times out and
  the app silently returns 0.
- Fix: when a newer version starts and finds an older one holding the mutex, tell the running copy to quit (QuitName) and take
  over, or show the update card. Find other copies by mutex owner or by image path/version (`Native.ExePath`), not by
  process name.

### M7 (Medium) - "Slower than usual" alert fires right after switching networks (the 5-minute window is global, not per network)
- src/Brain/Brain.cs:193, 205-212
```csharp
static readonly List<double> recentGw = new List<double>(), recentNet = ...;   // shared by every network
Keep(recentGw, gw); ...  if (b.N >= 120 ...) { double g5 = Avg(recentGw) ... if (g5 > Math.Max(b.Gw * 2.5, b.Gw + 15)) ... Alerts.Raise("base", ...)
```
- Scenario: the user tethers on a phone hotspot (gateway around 40 ms) and then gets home, where the learned home baseline is
  2 ms with N≥120. The first home minutes average 4 hotspot values with 1 home value. The result is "Slower than usual on
  Home - the link to your router is 20x slower than normal" as a timeline event plus a pop-up. NetKey lags by up to 60 s,
  which widens the window further.
- Fix: keep the recent window per key (or clear it when `key` changes) and require 4 samples from the current network.

### M8 (Medium) - History compaction can overwrite a day's compressed history; Load then ignores the .gz
- src/History.cs:212-218, 240-243
```csharp
if (d < DateTime.Today && n.EndsWith(".csv", ...)) { using (var dst = File.Create(f + ".gz")) ... src.CopyTo(z); File.Delete(f); }
...
if (File.Exists(file)) return File.ReadLines(file).Take(max);        // .gz never read if a .csv also exists
```
- Scenario: Brain stamps lines with `DateTime.Now`. A timezone change while travelling west across midnight, a manual clock
  correction backwards, or a dual-boot RTC correction makes lines get appended to yesterday's file after yesterday was already
  compressed. A new small `yesterday.csv` is created. Until the next Tidy, the History view shows only those few lines.
  Then Tidy runs `File.Create(yesterday.csv.gz)` (truncate), which replaces the whole compressed day with the fragment, and
  the day's history is lost.
- Fix: when `.gz` exists, append (decompress, concatenate, recompress to a temp file, then replace) instead of truncating.
  In `Lines`, read both and merge.

### M9 (Medium) - Offline or no-gateway states get the wrong verdict ("DNS is failing" / "All quiet" / "local network dragging")
- src/Monitor.cs:663-693 (with H2)
- Scenario A: boot with the cable unplugged, Wi-Fi off, or airplane mode. `gwEver=false` gives `link=3`. ProbeDns runs anyway
  and DnsQuery fails with a code other than 9003/9501, so `dn=2`, and the headline says "The internet's there but DNS lost
  the phone book". The internet is not there; there is no network at all.
- Scenario B: same, but the Internet check is Off. `dn=3`, so the headline is green "All quiet. Nothing to fix - go get
  coffee." while the timeline says "No network connection".
- Scenario C: offline after having been online leads to H2's "dragging" headline.
- An IPv6-only network has no IPv4 gateway, so `Primary=null`. That means "No network connection", no traffic counters, and
  no usage counted, while DNS works, so the result is green.
- Fix: put an explicit "not connected" verdict first (`Primary == null` or `Adapter == ""`, or `NetworkInterface.GetIsNetworkAvailable()`
  false). Skip ProbeDns when there is no gateway. Accept IPv6 default gateways for adapter selection.

### M10 (Medium) - Self-test writes simulated events into the user's real history
- src/SelfTest.cs:470-473 into Monitor.SimulateForTest (Monitor.cs:348-363), then Timeline.Add, then History.Event
  (Model.cs:116, History.cs:180-184)
- Scenario: a user (or build_exe.cmd, which runs `--selftest` after every build) runs the self-test. Today's
  `%LOCALAPPDATA%\AdeeboMeter\history\<today>-events.csv` gains fake entries: "VPN connected (Example VPN Client Adapter)",
  "Switched to LAN (Ethernet)", "Wi-Fi roamed... aa:bb:cc:00:00:02", "Wi-Fi disconnected", "IP address changed". These show
  up in the History or Changes view as if they happened.
- Fix: set `Cfg.History=false` (and `Cfg.Alerts=false`) for the whole self-test, or give Timeline a no-persist flag for test
  Monitors.

### M11 (Medium) - Release pipeline: the "built from this exact source, anyone can check" claim doesn't hold
- .github/workflows/build.yml:160-162, 169-173, 185-190; release.ps1:39, 94-102; build_exe.cmd:134
- The legacy `csc.exe` (v4.0.30319, the C# 5 compiler) has no `/deterministic` switch. Every build embeds a fresh MVID and a
  PE timestamp, so a user who runs build_exe.cmd never gets the published SHA256. build_exe.cmd:134 nonetheless says "compare
  it with SHA256SUMS published with the release".
- A `workflow_dispatch` with `tag` builds whatever ref the run was dispatched on (main by default, or any branch), not the
  tag. As long as `Program.Version` matches, it `--clobber`s the release's exe, zips and source zip with artifacts built from
  a different commit than the tag. The provenance attestation then says "this workflow" but not "this tag's commit".
- Fix: in the release job, check `git rev-parse HEAD == git rev-parse $tag^{commit}`, or have the build job check out
  `inputs.tag`. Reword the reproducibility claim to "verify with `gh attestation verify`" (which is valid). Drop the "compare
  your own build's hash" advice.

### M12 (Medium) - Cross-thread list races: writers and readers lock different objects
- src/Brain/Watch.cs:52-53, 84-85 (`Calls.Summaries` mutated under `lock(k)`); readers `lock (Calls.Summaries)` at
  FullView.Overview.cs:118 and Investigate.cs:159, or no lock at all (FullView.Insights.cs:68-69 enumerates with `.Select`).
- src/Brain/Watch.cs:190, 222-223 (`Devices.Joined` mutated under `Devices.k` on a Task thread); readers `lock (Devices.Joined)`
  (SecChecks.cs:467) or no lock (Insights.cs:74-75).
- src/Program.cs:186 (`Cfg.Pins` "UI thread only") is read every second on the sampler thread by `Pinned.Tick`
  (Watch.cs:244-247): `Count` then `[0]` can race a UI removal.
- Scenario: a call ends, or a 10-minute device check inserts a row, while the History page paints. The result is
  InvalidOperationException ("Collection was modified") or ArgumentOutOfRange in paint, which is logged as "UI error" and
  leaves a missing card that frame. Rare but real over days of uptime.
- Fix: lock on the list object itself in the writers (`lock (Summaries)`, `lock (Joined)`), or publish immutable snapshots
  (copy-on-write) as Monitor already does for `Apps` and `PidNames`.

### L1 (Low) - Upgrading silently re-enables autostart
- src/Setup.cs:149, 227 (`autoStart = true` default on the offer card)
- A user who had turned Start with Windows off runs "Update". The switch shows on (it isn't initialised from the existing
  settings), and `SetLine(settings, "autostart", "1")` overwrites their choice even though "Your settings come along".
- Fix: initialise the switch from the installed copy's settings or the existing Run value.

### L2 (Low) - Timeline / "ago" use local wall time: DST and clock changes give negative ages and suppress events
- src/Model.cs:113-118, 124-128
```csharp
var d = DateTime.Now - t;
return d.TotalSeconds < 60 ? ((int)d.TotalSeconds) + " s" : ...
```
- At the autumn DST change (or a manual time change, or a timezone change while travelling), entries from the last hour read
  "-3540 s". The 120 s dedupe (`(now - last.T).TotalSeconds < 120` is true for negative spans) suppresses repeated identical
  events for up to an hour. "Internet back after -59 min" (Monitor.cs:235).
- Fix: store `DateTime.UtcNow` (or a monotonic tick) for age maths, convert only for display, and clamp at 0.

### L3 (Low) - A corrupt or planted history line makes the weekly heat map throw
- src/History.cs:263, 291
```csharp
TimeSpan.TryParse(f[0], CultureInfo.InvariantCulture, out t) ... row[0] = t.TotalSeconds;
int h = Math.Min(23, (int)(r[0] / 3600));   // "-01:00:00" -> h = -1 -> lv[d, -1] IndexOutOfRangeException
```
- `Week()` has no try/catch, so the History page's heat map fails on every paint. Fix: reject `t < 0 || t >= 1 day` in Load.

### L4 (Low) - Log: no rate limit, full wipe at 256 KB, culture-dependent timestamps, unescaped newlines
- src/Program.cs:129-141; AppCtx.cs:340 (`OnUi` logs every 50 ms on a persistent error); Monitor.cs:139, 249.
- A persistent exception in `OnUi` writes 20 lines/s: open/append/close plus Defender scanning in Downloads. Then
  `File.Delete` at 256 KB throws away the earliest (most useful) lines every few minutes.
- `DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CurrentCulture)` gives the Buddhist year (th-TH) or "." as the time separator
  in some cultures.
- Strings from the network (reverse-DNS names via "provider: ", exception messages) go in unescaped.
- Fix: rate-limit identical messages, roll to `.log.1` instead of deleting, use InvariantCulture, and replace CR/LF in `m`.

### L5 (Low) - Privacy and doc claims don't fully match the code
- src/Program.cs:3-8. The header lists "the only packets it sends" but leaves out the per-network 6-hop traceroute toward
  1.1.1.1 plus reverse DNS (Monitor.cs:409-431; About/Settings do disclose it) and the DNS resolution of a pinned hostname
  every 30 s (Watch.cs:250 into `PingMs(host)`). It also says "Everything it keeps (settings, log) lives next to the exe;
  captures go to %LOCALAPPDATA%", but history, usage (400 days), baselines, device MACs, seen-apps and device-nets all
  persist in `%LOCALAPPDATA%\AdeeboMeter`.
- src/SelfTest.cs:31 says self-test network use is "nothing on the internet". But `DestCheck.Run(gw + ":80")` (SelfTest.cs:219)
  calls `Portal()` when the check isn't clean (DestCheck.cs:96), and that requests www.msftconnecttest.com. A router without
  port 80 is common.
- `Dns.GetHostEntry(IPAddress)` on .NET Framework does a reverse lookup, and also a forward lookup of the returned name (see
  Suspected S4).

### L6 (Low) - Uninstall leaves `%TEMP%\AdeeboMeter-remove.exe` behind for good
- src/Setup.cs:196-201, 209-219. The helper deletes `Base` but never itself. Fix: launch it with
  `FILE_FLAG_DELETE_ON_CLOSE`-style self-delete (for example `cmd /c ping -n 3 127.0.0.1 >nul & del`, against the
  "never through cmd" rule), or have the next install/start sweep it, or use `MoveFileEx(..., MOVEFILE_DELAY_UNTIL_REBOOT)`.

### L7 (Low) - SHA256SUMS inside the zip uses CRLF
- release.ps1:54 writes `"$exeSum  AdeeboMeter.exe`r`n"`. On Git Bash, WSL or Linux, `sha256sum -c SHA256SUMS` looks for
  `AdeeboMeter.exe\r` and fails. The outer dist/SHA256SUMS uses LF, so the two disagree. Fix: use LF in both.

### L8 (Low) - Crash-report URL can exceed GitHub's URL limit
- src/Brain/Watch.cs:273-277. Up to 3500 chars of stack trace are percent-encoded (newlines, spaces and colons each become 3
  chars), giving ~8-10k-char URLs. GitHub's new-issue page answers 414/400 for very long query strings, so "Click here to
  report it" lands on an error. Fix: cap the encoded body at ~6000 chars.

### L9 (Low) - `Crash.OpenUrl` prefix allowlist is loose
- src/Brain/Watch.cs:280. `StartsWith("https://github.com/adeeb111/AdeeboMeter")` also accepts `...AdeeboMeterEvil/...` or
  `...AdeeboMeter@evil`. Today only hardcoded URLs reach it. Fix: parse with `Uri` and compare host plus path segment
  exactly.

### L10 (Low) - Adapter choice ignores Windows' route metric
- src/Monitor.cs:286-296. Any Ethernet with an IPv4 gateway beats Wi-Fi. A USB-Ethernet link to a lab switch, a Raspberry Pi
  or a camera network that hands out a gateway becomes "Primary", while the internet actually goes over Wi-Fi. The app then
  pings the wrong gateway, reads the wrong traffic counters, and uses the wrong NetKey. `NetworkInterfaceType.Tunnel`
  adapters are skipped before `IsVpn`, so some VPNs aren't recognised. Fix: pick the interface of the best default route
  (`GetBestInterface(1.1.1.1)` / `GetBestRoute2`).

### L11 (Low) - Settings writes use `File.Replace`, which fails on many SMB/NAS shares and some removable drives
- src/AppCtx.cs:213, src/Brain/Brain.cs:110. On failure the error is only logged, `.tmp` is left behind, and settings never
  update (portable use from a USB stick or NAS). Fix: on IOException fall back to delete plus move.

### L12 (Low) - Other small items
- `--write-icon <path>` (Program.cs:99) has no try/catch, so a bad path is an unhandled crash that leaves a crash note. Also
  `--minutes`, `--view`, `--tip`, `--dest`, `--remove-folder` and `--write-icon` match case-sensitively, unlike the other
  flags. A flag with its value missing is ignored silently. Not injectable (nothing is passed to a shell).
- Any args (for example `--dark`) suppress the first-run install offer (Program.cs:103), which is fine but undocumented.
- `Single.KillOthers` / `StopOthers` (Program.cs:275-282, Setup.cs:87-100) enumerate processes in all sessions. With
  fast-user-switching another user's copy can't be killed, so every install waits the full 4 s.
- `Store.Read(...).Take(20000)` (Brain.cs:100): usage.txt is appended oldest-first, so past 20k rows the newest rows (current
  month) are the ones dropped on reload.
- `Watch.Conns` keys "first time on the internet" by full exe path (Watch.cs:140-151). Apps with versioned install paths
  (new Teams under WindowsApps\MSTeams_<ver>, Discord app-<ver>, many Squirrel/updater apps) raise a level-1/2 "connected to
  the internet for the first time" pop-up after every update. Anything under `\Temp\` is level 2 even during the learning
  day (legit updaters run from Temp).
- `Brain.Bg` (Brain.cs:76-81) creates a new OS thread every 5 s (~17k threads/day). Use one long-lived low-priority worker.
- `Alerts.OnEvent` "inet" text says "Your router still answers" (Watch.cs:39) without checking the gateway.
- `Usage`/`Baseline` key by gateway MAC when known, else gateway IP. A failed ARP read flips the key and splits one network
  into two rows/baselines. Usage while `Gateway==""` (IPv6-only, some VPNs) is booked to "Offline".

---------------------------------------------------------------------------------------------------------------------------

## B. Suspected (needs a Windows box to confirm)

- S1 (Medium) - Defender / SmartScreen heuristics. The unsigned exe copies itself into `%LOCALAPPDATA%\Programs`, writes the
  Run key, copies itself to `%TEMP%` and runs it with `--remove-folder <pid>`, kills processes by name, enumerates all
  processes' image paths and reads the DNS cache. That matches common ML false-positive patterns (Wacatac/Sabsik). Also
  `File.Copy` keeps the `Zone.Identifier` ADS, so the installed copy still carries Mark-of-the-Web. Worth testing on a clean VM
  with cloud protection on.
- S2 (Medium) - Localized netsh output. The exact label strings (German "Status"/"Kanal", Spanish "Señal"/"Estado", French
  "État" with a non-breaking space before "%") are from memory. The code is certainly English-only (M2).
- S3 (Low) - `PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total")` with English names on localized Windows or with
  corrupted perf-counter registry. The failure is handled (Disk shows NaN), but the first construction can block the sampler
  thread for several seconds at start.
- S4 (Low) - `Dns.GetHostEntry(IPAddress)` on .NET Framework follows the PTR with a forward A/AAAA lookup of the returned
  name: one more undisclosed query per network. The `t.Wait(3000)` abandons, but does not cancel, a hung resolver thread.
- S5 (Low) - Paths longer than MAX_PATH (deep OneDrive or corporate folder trees). Target 4.8 with no `longPathAware`
  manifest, so `File.*` next to the exe may throw PathTooLongException. Settings and log fail silently (same symptom as M4).
- S6 (Low) - SystemEvents handlers in AppCtx (`UserPreferenceChanged`, etc.) are raised on a SystemEvents thread when the main
  thread has no message loop at subscription time. Here they are subscribed in the AppCtx ctor before `Application.Run`, so
  `prefHandler` may run `Theme.Apply` and `full.ApplyTheme()` off the UI thread. Check by switching the Windows theme with
  the engineer view open.

---------------------------------------------------------------------------------------------------------------------------

## C. Test coverage gaps (SelfTest.cs)

Exit-code semantics are right: `Run()` returns the FAIL count, and the workflow and build_exe.cmd both treat non-zero as
failure. But many network items only check "didn't throw". On a CI runner, "gateway ping", "health check", "interfaces" and
"destination check" (skipped with no gateway) pass whatever the result. "never listens" is vacuous because AppCtx is never
constructed, so nothing that could listen is running.

Untested, and covering most findings above:
1. **Verdict logic (`Monitor.Judge`)** - no table-driven cases for offline/airplane, gateway ignores ICMP, gateway that once
   replied, IPv6-only, Internet check off, or Wi-Fi without netsh. Only one simulated hint is asserted.
2. **Setup** - Install/Uninstall/upgrade/downgrade never run. Only `Under()` and `SetLine()` are tested. There are no tests
   for Run-key quoting, "run here" autostart behaviour, settings migration, or the old-version cleanup.
3. **Settings file I/O** - `LoadSettings`/`SaveSettings` are not tested; only in-memory `Cfg.Read`. There are no
   garbage/empty/huge/read-only file cases.
4. **History** - Minute/Event write, Tidy (compaction, rolling delete, .csv + .gz coexisting), Load parsing of
   corrupt/negative/future lines, and Week() are untested.
5. **Brain** - Usage (day/month sums, 400-day cut, cap alert), Baseline (learning, false alerts after a network switch), Bloat
   and Calls.Summarise are untested.
6. **Watch / Devices / Alerts** - first-seen apps, night rule, device learning period, alert cooldowns and quiet hours are
   untested.
7. **ProbeWifi netsh parser** - not tested at all, neither English nor localized nor location-blocked (only LinkInfo's
   parsers are).
8. **Ring / Timeline** - wrap-around, NaN handling, `Ago()` with negative spans, the dedupe window.
9. **Clock robustness** - nothing exercises clock jumps (H4).
10. **CLI argument parsing and single-instance hand-off** - no tests.
11. **Crash.Clean / IssueUrl** - no tests for user-name scrubbing or URL length.
