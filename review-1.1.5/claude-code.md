# Findings - Claude Code

Reviewed commit: f6d5747 (dev-1.1.5). Test build: AdeeboMeter.exe, sha256 c3732a6b9529e26e94c181047904e65ff834a3f80cde6af19a092ee338dacdb1.

Method:
1. Static read of every file in src\, knowledge\, docs\, store\, the build scripts and the workflow at f6d5747; every 1.1.4 finding was re-checked against dev-1.1.5 and is reported here at its dev-1.1.5 line.
2. Mono 6.8 builds of the source (mcs, C# 5) for harnesses; the website was driven with Playwright/Chromium and axe-core 4.10.2.
3. Reflection harnesses that load the shipped test build (portable mode) and call its internal methods, plus hostile local servers (SMTP, SNMP agent, iperf3) and `--report` runs of the test build.
4. Fuzzing, 0 crashes unless listed below: SNMP decoder 330,000 packets (+ a malicious agent driving the real Walk); pcap/pcapng reader + all four replays 530,000; RADIUS replies 200,000; tshark/netsh/WLAN-IE/knowledge parsers 20,000 per target (40,000 IE blobs); DNS/STUN/varbind/DHCP/rootDSE parsers 15,000 each; .adeebo all 44 header-byte flips plus truncation/append, and 20,000 generated codes.

"Confirmed" means I ran it: under mono on Linux (not Windows) against the test build or a build of the same code, by a harness, or (website and docs) in Playwright or by checking the file content. "Suspected" means I read it only; most of those depend on Windows behaviour mono doesn't have.

## Rule breaks

| Rule | Findings |
|---|---|
| 1. Offline (only what the user ran or About lists) | #22, #23, #28, #39 |
| 2. Never listens | #38 (owner to decide) |
| 3. No admin | none found |
| 4. Secrets never kept | #16, #17 |
| 5. SNMP read-only | none found |
| 6. Policy only switches things off | #13, #14, #15 |
| 7. Low weight (64 MB, no busy loops, no idle repaints) | #172, #173, #174, #178, #179, #180, #181, #182, #183, #184 |
| 8. No Nokia references | #189 |
| 9. Examples are made-up data | #75, #76 |
| 10. Points can't be farmed | #71, #72, #73, #74 |
| Brief guards (Traffic Lab one target, 10/s, company tick; MTR loss rule) | #24, #25, #26, #44 |

## Top 15 to fix before release

1. #13 - an empty AdeeboMeter.policy next to the exe cancels IT's policy (rule 6, confirmed).
2. #14 - policy `mode = pro` turns Pro on and locks it (rule 6, confirmed).
3. #15 - speed test `check` ignores `speed_test = off` (rule 6).
4. #43 - version still 1.1.4: Store package, release check and --report all wrong (confirmed).
5. #1 - iperf3 `-u -l 4` kills the app (confirmed).
6. #2 - hostile mail server: 6 GB / OOM / endless hang, now reachable from SMTP path for any domain (confirmed).
7. #3 - corrupt points.txt breaks every tab switch (confirmed).
8. #4 - nameless custom game breaks Gamer Zone on every paint (confirmed).
9. #44 - MTR and the ISP report blame the provider for destination-only loss (confirmed).
10. #45 - a URL target breaks the ping/trace tools and makes the VPN guide green (confirmed).
11. #52 - worst bufferbloat graded "A+" and repeated in the ISP report (confirmed).
12. #189 - "On SR OS" in the TWAMP hint (rule 8, confirmed, one-line fix).
13. #16 - decrypt code kept in the last result, unmasked box (rule 4).
14. #20 - uninstall deletes key backups while private keys stay; encrypted files become unopenable.
15. #19 - ISP report redaction breaks the HTML, misses escaped SSIDs and keeps LAN host names (confirmed).

## Findings

### 1. iperf3 UDP with a small -l kills the whole app
Severity: crash  
Where: src\Checks\Toolkit.cs line 57 (ParseOpts, "-l" has no lower bound), lines 227 and 240 (IperfOne stream thread)  
How to reproduce: Toolbox > iperf3, Advanced = `-u -l 4` (any -l from 1 to 11 with -u, upload direction), against any iperf3 server.  
What happens / what should happen: each stream thread writes a 12-byte header into a smaller buffer and throws IndexOutOfRangeException on a bare `new Thread`, outside StartTool's catch, so the process dies. Related: `-u -l 1M` swallows every SocketException(MessageSize) and reports about 0 Mb/s as a success. Clamp UDP length to 12..65507, catch everything in the thread and show it as a row.  
Confidence: confirmed (ran it: fake iperf3 server + mono build of the same code, "FATAL UNHANDLED EXCEPTION: IndexOutOfRangeException at Toolkit.Be", exit 1)  

### 2. Mail check and SMTP path: a hostile mail server can exhaust memory or hang the tool forever
Severity: crash  
Where: src\Checks\Protocols.cs lines 184-199 (Mail: unbounded ReadLine and caps list); src\Checks\EngPack.cs line 351 (SmtpPath now points Mail at the MX of any domain typed)  
How to reproduce: a server that sends "220 hi" then endless `250-XXXX` lines, or 64 KB chunks with no line end, or one byte every 5 s; then Toolbox > Mail check (or SMTP path) against it.  
What happens / what should happen: endless 250- lines: 40 s, 6.4 GB peak working set; no CRLF: OutOfMemoryException after 11 s at 4.2 GB; drip: still running after 45 s because the 8 s ReadTimeout is per read, and Stop is never checked. Cap each line (1 KB) and the EHLO line count (50), add an overall deadline (about 20 s) and honour run.Stop.  
Confidence: confirmed (ran it: srv.py hostile server against the 1.1.5 test build under mono)  

### 3. A corrupt points.txt throws in the Points constructor and breaks every tab switch
Severity: crash  
Where: src\Points.cs lines 43-55 (Load) and 38 (Points.Me); callers src\Ui\FullView.Points.cs line 98 (SyncPointsBtn), src\Ui\FullView.cs line 391 (ShowTab2)  
How to reproduce: put `mtr|9223372036854775807|-|2` in data\points.txt (any ticks above 3155378975999999999 in field 2 or 3; a bit flip is enough), start, switch tabs.  
What happens / what should happen: `new DateTime(ticks)` throws ArgumentOutOfRangeException, which Load does not catch, so Points.Me is never assigned and every later access throws again. SyncPointsBtn runs at the end of every ShowTab2 (even with Points off), so each tab switch stops part-way and logs "UI error". Skip bad lines (catch per line, clamp ticks).  
Confidence: confirmed (ran it: constructor throw reproduced on the test build under mono); the tab-switch effect is read  

### 4. A custom game with an empty name makes the Gamer Zone page throw on every paint
Severity: crash  
Where: src\Gamer\Gamer.cs line 107 (ApplyFile: `if (nm.Length > 0) g.Name = nm;` leaves Name null); src\Ui\FullView.Gamer.cs line 95 (`gm.Name.Substring(0, 1)`)  
How to reproduce: games.txt containing `game|my-x|||` (or a name made only of characters Clean() strips), then open Gamer Zone.  
What happens / what should happen: Games.All returns a game with Name == null and the tile loop throws NullReferenceException on every repaint, so the page stays broken. A name starting with an emoji also draws half a surrogate pair in the tile icon. Skip nameless custom games or default the name to the id; take the first text element for the icon.  
Confidence: confirmed (ran it: test build under mono, `my-x name=<null>`, Substring throws); the paint failure is read  

### 5. One malformed line in keys-ec.txt breaks My keys, opening P-384 files and "also for me"
Severity: crash  
Where: src\Checks\EcKeys.cs line 43 (All: no base64 check) and line 50 (Save: non-atomic WriteAllLines); src\Checks\CertKeys.cs line 102; src\Checks\FileCrypt.cs line 213; src\Ui\FullView.Toolbox.cs line 290 (backup thread)  
How to reproduce: add `ec|Bad|K|!!notbase64|202601010000` to keys-ec.txt (or cut the file mid-write with a crash or power loss), then open My keys or a mode-2 .adeebo.  
What happens / what should happen: CertKeys.MyKeys() and Decrypt throw FormatException ("Couldn't finish"). The EC backup thread rethrows anything outside IO/Unauthorized/Crypto on a raw Thread, so the same bad line there would end the process. Validate base64 and the 104-byte length in All(), write atomically (temp + replace).  
Confidence: confirmed (ran it: harness h3 on the test build, MyKeys THROWS FormatException); the backup-thread path is read  

### 6. Wi-Fi and advisor parsers throw on odd netsh output and drop the whole page
Severity: crash  
Where: src\Link\LinkInfo.cs line 373 (ParseNetworks int.Parse), line 253 (Arp.Randomised Convert.ToInt32); src\Knowledge\Explain.cs line 115 (CloseText[e.Close])  
How to reproduce: netsh line `Channel Utilization: 1 (99999999999 %)`; a BSSID with non-hex characters; a knowledge entry with `close: 4`.  
What happens / what should happen: OverflowException loses the whole "Your connection" snapshot; FormatException in Randomised makes WifiAdvisor.Fill throw so all advice is lost; close >= 4 throws inside a Paint handler. Use TryParse and clamp the index.  
Confidence: confirmed (ran it: mono fuzz harness, 20,000 inputs per target: 130 OverflowException hits, FormatException on non-hex)  

### 7. A corrupt or out-of-range history row breaks the heat map and Replay on every paint
Severity: crash  
Where: src\History.cs line 131 (Load: no t<0 / t>=1 day check) and line 159 (Week: `lv[d, h]` with h = -1); src\Ui\FullView.Replay.cs lines 50 and 126 (Paint: AddSeconds)  
How to reproduce: put a row starting `-01:00:00,` or `10675199.02:48:05,` into a day's history .csv, open History (week heat map), Insights, or Replay for that day.  
What happens / what should happen: Week() throws IndexOutOfRangeException (no try/catch, and Insights retries it on every paint), Replay's AddSeconds throws ArgumentOutOfRangeException inside Paint on every repaint; if History.Load throws, Replay shows "Reading that day..." forever. DST fall-back days repeat an hour that Replay can't reach. Drop rows outside 0..86400 s at load; set a load-error state.  
Confidence: suspected (read it; TimeSpan.TryParse accepting both rows was run under mono)  

### 8. Command-line flags: --write-icon crashes on a bad path; several flags are case-sensitive
Severity: crash  
Where: src\Program.cs line 118 (--write-icon, no try/catch) and line 111 (--render-card, --seconds)  
How to reproduce: `AdeeboMeter.exe --write-icon Z:\nope\x.ico`; or `--VIEW toolbox`, `--Seconds 30`.  
What happens / what should happen: the first is an unhandled exception (crash note written). --minutes, --view, --tip, --dest, --remove-folder, --write-icon, and the new --render-card and --seconds match case-sensitively, unlike the others; a flag with a missing value is ignored silently. Catch IO errors and compare flags case-insensitively.  
Confidence: suspected (read it)  

### 9. Game server names that collide after 30-character truncation make Side-by-side throw
Severity: crash  
Where: src\Gamer\GameCompare.cs line 70 (ToDictionary on Name); src\Gamer\Gamer.cs line 117 (SaveGame writes Clean(s.Name, 30)), line 377 (Plan keys on Name)  
How to reproduce: in Edit, leave Name blank for two servers whose hosts share their first 30 characters (`eu-west-gameservers.provider.example.com` and `.net`), save, reload, run Side-by-side.  
What happens / what should happen: ArgumentException "An item with the same key has already been added" gives "Couldn't finish", and the second server is never surveyed. De-duplicate names on load and save.  
Confidence: suspected (read it)  

### 10. RADIUS lab: one corrupt aaa-runs.txt line breaks the tool on every later run
Severity: crash  
Where: src\Checks\EngPack.cs line 322 (DateTime.ParseExact on p[2])  
How to reproduce: an `aaa|host:port|notadate|...` line with 6 or more fields that becomes prevSame or prevOther; run the RADIUS lab again for that server.  
What happens / what should happen: FormatException after the request was sent, so the user never sees the result, and the bad line is kept (it stays in the last 30). Use TryParseExact and skip the line.  
Confidence: suspected (read it)  

### 11. Gamer widget card can dereference a null target
Severity: crash  
Where: src\Ui\Theme.cs line 258 (Layout checks GamerTarget() != null) and line 274 (GamerCard calls GamerTarget() again and uses t.Item1)  
How to reproduce: game widget on; the game ends (GamePing.Me.Playing changes on the ping thread) or Games.Reload drops the server between the two calls.  
What happens / what should happen: NullReferenceException in the widget render. Pass the target from Layout into GamerCard or null-check and fall back to the everyday card.  
Confidence: suspected (read it; a race)  

### 12. Paint watchdog timer can crash the process while the view closes
Severity: crash  
Where: src\Ui\FullView.cs line 76 (watchdog Timer callback, only InvalidOperationException caught)  
How to reproduce: close the engineer view repeatedly; the handle can be destroyed between the IsHandleCreated check and BeginInvoke.  
What happens / what should happen: an ObjectDisposedException escapes a threadpool timer callback, which ends the process (crash note, "closed unexpectedly" balloon next start). The callback can also run after watchdog.Dispose(). Catch Exception there.  
Confidence: suspected (read it; tiny race, runs every second)  

### 13. An AdeeboMeter.policy next to the exe silently overrides IT's %ProgramData% policy and switches everything back on
Severity: security  
Where: src\Policy.cs lines 34-41 (Policy.Load: first file found wins, then return)  
How to reproduce: IT sets `internet_check = off`, `speed_test = off`, `traffic_lab = off`, `live_capture = off`, `mode = simple` in C:\ProgramData\AdeeboMeter\AdeeboMeter.policy. The user (or any user-level script) creates an empty AdeeboMeter.policy in the exe folder (the installed exe lives in user-writable %LOCALAPPDATA%\Programs\AdeeboMeter\<ver>\).  
What happens / what should happen: with only the admin file, NoInternetCheck/NoSpeed/NoLab/NoCapture are True and Mode=simple; with the empty exe-dir file added, all are False and Mode is empty, and Settings is unlocked. Read both files and OR them (strictest wins), or trust only %ProgramData%.  
Confidence: confirmed (ran it: harness h2 on the test build under mono, before/after values above)  
Rule: 6  

### 14. Policy line `mode = pro` switches Pro mode on for a user who chose Simple, and locks it
Severity: security  
Where: src\Policy.cs line 63 (case "mode") and line 72 (Apply); comment at line 23 says pro "doesn't add anything"  
How to reproduce: user chose Simple; put `mode = pro` in AdeeboMeter.policy (exe folder or ProgramData).  
What happens / what should happen: Cfg.Mode becomes pro, every Pro tab (Monitor, Security, Toolbox, Traffic Lab) appears, and Settings > Mode is locked "Set by your IT team". A policy may only narrow: accept only `mode = simple`.  
Confidence: confirmed (ran it: harness h2 on the test build, "user chose simple -> Cfg.Mode after Apply = pro")  
Rule: 6  

### 15. Speed test "check" contacts every server even when the policy turns the speed test off
Severity: security  
Where: src\Checks\SpeedTest.cs line 101 (Run -> Servers(check)) and line 128 (Head); Policy.NoSpeed is only tested in Measure (line 153)  
How to reproduce: AdeeboMeter.policy with `speed_test = off`; Toolbox > Speed test, type `check`, Run.  
What happens / what should happen: about 20 HTTPS range requests go out (Cloudflare, 17 Vultr cities, 2 OVH). Run should return the policy row before both list/check and Measure.  
Confidence: suspected (read it; direct path, network not reachable from the harness)  
Rule: 6  

### 16. The decrypt tool's code or key-backup passphrase is kept in the last-result cache, in an unmasked box
Severity: security  
Where: src\Ui\FullView.Toolbox.cs line 198 (SecretInput matches only "Shared secret", "Community", "Password"), line 124 (decrypt label "Code - ..."), lines 661-669 (tbLast/tbPrev Inputs and Shown)  
How to reproduce: Toolbox > Encrypt or decrypt a file, open a code-mode .adeebo (or restore a key backup) with its code; look at the previous-run state (Inputs/Shown).  
What happens / what should happen: Copy and Save filter "Code", but the code stays in tbLast/tbPrev for the whole session and is typed into a plain text box. The per-row "copy:" for a generated code goes through Clipboard.SetText, so it lands in Win+V history and Cloud Clipboard. Add `label.StartsWith("Code")` to SecretInput (or blank shown[1]), mask the box, and set the exclude-from-clipboard-history formats.  
Confidence: suspected (read it)  
Rule: 4  

### 17. "Inspect a certificate" may write a private key to the user key store
Severity: security  
Where: src\Checks\CertTools.cs line 88 (`new X509Certificate2(b)` with default key storage flags)  
How to reproduce: Inspect a certificate on a DER file that is really a PFX with an empty password.  
What happens / what should happen: the key is imported and persisted under %APPDATA%\Microsoft\Crypto, contradicting "private keys are never written anywhere". Use EphemeralKeySet or reject PKCS#12 content before constructing.  
Confidence: suspected (read it; needs Windows)  
Rule: 4  

### 18. Live-capture ring and sniff folders survive a crash, kill, power loss or logoff (full-payload traffic left on disk)
Severity: security  
Where: src\Program.cs lines 185-187 (CleanCaptures: *.pcapng in the top folder only, not recursive); src\AppCtx.cs lines 66 and 283; src\Capture\LiveCapture.cs line 360 (sniff_HHmmss folders)  
How to reproduce: start Live capture, then end AdeeboMeter from Task Manager (or crash, power off, log off); start it again and look in %LOCALAPPDATA%\AdeeboMeter\captures\ring\ and sniff_*.  
What happens / what should happen: up to about 100 MB of unfiltered payload (DNS, HTTP, SMB...) stays until Live capture is started again, contradicting "deleted on exit" (THEORY:364). Clean recursively at start and on SessionEnding.  
Confidence: suspected (read it)  

### 19. ISP report and game-session redaction corrupts the HTML, misses escaped Wi-Fi names, and keeps LAN host names
Severity: security  
Where: src\Gamer\Gamer.cs line 611 (GameSession.Redact, plain substring replace); applied to the finished HTML at src\Checks\IspReport.cs line 294  
How to reproduce: Redact with SSID `td` (or tr, div, table, body, class), SSID `ms`, SSID `AT&T WiFi`; a hop list with private hops that have reverse-DNS names.  
What happens / what should happen: SSID `td` turns `<td class="n">` into `<[Wi-Fi name] class="n">` (broken saved report); `ms` replaces every "ms" unit (revealing the SSID by elimination); `AT&T WiFi` is not redacted because the HTML has `AT&amp;T`. Private hops keep their PTR names ("Johns-MacBook.lan ([internal address])"); fe80::1%12 keeps "%12"; 100.64/10 and EUI-64 IPv6 (embeds the MAC) pass. Redact row values before HTML encoding, whole-word SSID only, replace private hop names.  
Confidence: confirmed (ran it: Redact on the test build under mono)  

### 20. Uninstall deletes key backups and the P-384 key index but leaves the private keys; after reinstall your files can't be opened
Severity: security  
Where: src\Setup.cs line 192 (Directory.Delete of %LOCALAPPDATA%\AdeeboMeter, recursive); src\Checks\EcKeys.cs line 27 (keys-ec.txt) and line 135 (Decrypt tries only indexed keys); src\Checks\CertKeys.cs line 27 (OutDir = DataDir\certs)  
How to reproduce: My keys > New key (P-384) > Back up private key; uninstall from the tray or Settings > Apps; install again; open a .adeebo sent to you.  
What happens / what should happen: uninstall says "Removing your history and captures..." but deletes certs\ (all .pfx and key-backup.adeebo), recipients\ and keys-ec.txt, while the CNG keys and CurrentUser\My RSA keys stay in Windows. After reinstall every file fails with "this file wasn't made for any of your keys" and the backup is gone. Save backups via a SaveFileDialog, ask before deleting keys, find CNG keys by prefix.  
Confidence: suspected (read it; plain code path, Windows deletion not run)  

### 21. Certificate commands: PowerShell injection through Unicode smart quotes in a file name
Severity: security  
Where: src\Checks\CertTools.cs line 301 (Unsafe char list) and lines 313-319 (command builder)  
How to reproduce: Certificate commands on a file named `C:\certs\a” (calc) ”b.cer` (U+201D is legal in NTFS names), copy the row, paste into PowerShell.  
What happens / what should happen: the builder accepts it and emits `certutil -dump "C:\certs\a” (calc) ”b.cer"`; PowerShell treats ” as a quote, so `(calc)` runs. UNC paths (`\\attacker\s\x.cer` + `certutil -urlfetch`) leak NTLM. Whitelist characters, reject Unicode quote marks and `\\` paths.  
Confidence: confirmed (ran it: mono harness on the 1.1.4 build accepted the path and emitted the command; file unchanged in 1.1.5)  

### 22. "Router ping: Off" is ignored while the engineer view is open
Severity: security  
Where: src\Monitor.cs line 212 (`gwSec = ViewOpen ? 2 : Cfg.GwSec`)  
How to reproduce: Settings > Router ping = Off (About then says "Off."), open the engineer view, watch traffic to the gateway.  
What happens / what should happen: the gateway is pinged every 2 s while the view is open. NetSec handles Off correctly; GwSec should be `Cfg.GwSec == 0 ? 0 : ViewOpen ? 2 : Cfg.GwSec`.  
Confidence: suspected (read it)  
Rule: 1  

### 23. Painting Settings briefly rewrites live monitor intervals, so an "Off" probe can fire
Severity: security  
Where: src\Ui\FullView.Settings.cs line 221 (JobsFor called 3 times per paint) and lines 248-254 (JobsFor writes Cfg.GwSec/NetSec/WifiSec/AppsSec, then restores)  
How to reproduce: set a probe to Off, open Settings and scroll or resize it while the monitor loop runs.  
What happens / what should happen: the Monitor thread can read the Penny/Balanced/Engineer values mid-paint and schedule a ping or netsh that the user switched off. Compute job counts from a preset table without touching Cfg.  
Confidence: suspected (read it; a race)  
Rule: 1  

### 24. Traffic Lab accepts broadcast, multicast and 0.0.0.0 as "one target"
Severity: security  
Where: src\Checks\Lab.cs lines 42-48 (Split), 81 (ICMP regex), 137-140 (new ICMP dscp/compare mode, raw Icmp.Echo), 157-162 (UDP send)  
How to reproduce: udpsend to `255.255.255.255:9` x 1000; ICMP builder or `dscp ... compare` to `224.0.0.1`; udpsend to `192.168.1.255:9`.  
What happens / what should happen: Split returns True for 255.255.255.255:9 and UdpClient.Connect sets SO_BROADCAST itself, so every LAN host gets 1000 packets; multicast reaches every listener (Ping to multicast counts each reply). A directed broadcast fails with WSAEACCES and is reported as "port closed (the host said so)". Reject broadcast, Any, multicast, directed broadcast and 0/8 after resolving; count send errors separately.  
Confidence: confirmed (ran it: mono harness, `[255.255.255.255:9] -> True`); the delivery behaviour is read  

### 25. Traffic Lab UDP sender's 10 packets-a-second cap is bypassed against a closed port
Severity: security  
Where: src\Checks\Lab.cs lines 160-169 (UDP send loop: the gap is "receive until", and ConnectionReset breaks out early)  
How to reproduce: udpsend to `10.0.0.5:9` (a closed port on a host that answers ICMP port-unreachable), count 1000.  
What happens / what should happen: each ICMP unreachable makes the next Receive throw WSAECONNRESET and `break` skips the rest of the 100 ms gap, so 1000 packets go at LAN round-trip speed, not 10/s as the UI and brief promise. Pace with an absolute schedule independent of the receive loop.  
Confidence: suspected (read it; needs Windows ICMP behaviour)  

### 26. Security checkup bypasses the company-network gate and probes the router without the tick
Severity: security  
Where: src\Checks\SecChecks.cs line 408 (`Lab.DhcpOptions(m, "yes")` with the tick forced), lines 432 and 443 (RouterAdmin/RouterOld TCP probes)  
How to reproduce: on a domain or VPN PC, run Security checkup without ticking "my own network".  
What happens / what should happen: it sends a DHCP information request the Toolbox would refuse, and TCP-probes 80, 443, 21, 23, 7547, 8080 and 22 on the (company) gateway, while the page says "Reads settings only". Policy.NoLab is honoured (Lab.cs line 36). Pass the real tick or skip these checks with "needs your OK on a company network".  
Confidence: suspected (read it)  

### 27. Interception roots installed in the machine store are never flagged
Severity: security  
Where: src\Checks\SecChecks.cs lines 216-223 (only CurrentUser-only roots are compared with the Interceptors list)  
How to reproduce: a PC with Superfish, an antivirus web shield, Fiddler "trust for all users" or an admin-installed mitmproxy root in LocalMachine\Root; run Security checkup.  
What happens / what should happen: green "only the ones Windows and your company put there". Run the Interceptors match over LocalMachine roots too and drop the claim about who put them there.  
Confidence: suspected (read it)  

### 28. Files planted next to a downloaded exe (portable marker, settings, policy) redirect data and pre-set consent
Severity: security  
Where: src\Program.cs lines 163 and 170 (portable marker and settings read from the exe folder); src\Setup.cs (ShouldOffer)  
How to reproduce: a drive-by download puts `AdeeboMeter.portable` or `AdeeboMeter.settings` (with `gmon=` game ids) into Downloads next to AdeeboMeter.exe; run the exe.  
What happens / what should happen: history, keys-ec.txt, recipients\ and certs\ (key backups) go to Downloads\data\ (often OneDrive-synced); a planted settings file is read as the user's, which suppresses the install offer and can pre-set background game pings (gmon counts as consent). Honour these files only outside known download folders, or ask once.  
Confidence: suspected (read it)  
Rule: 1  

### 29. Release pipeline: "built from this exact source, anyone can check" doesn't hold, and a dispatch can publish a different commit
Severity: security  
Where: .github\workflows\build.yml line 28 and lines 58-85 (workflow_dispatch with tag builds the dispatched ref, then uploads with --clobber); build_exe.cmd line 21  
How to reproduce: dispatch the workflow with `tag=v1.1.5` from main or any branch whose Program.Version matches; or rebuild locally with build_exe.cmd and compare with SHA256SUMS.  
What happens / what should happen: the release's exe and zips are replaced with artifacts from another commit; csc 4.0 has no /deterministic, so a local build never matches the published hash, yet build_exe.cmd tells users to compare. Check out inputs.tag (or verify HEAD == tag), and point users to `gh attestation verify` instead. The Store bundle is also not covered by the attestation.  
Confidence: suspected (read it)  

### 30. The corner widget shows over full-screen presentations and is captured by screen sharing
Severity: security  
Where: src\AppCtx.cs lines 297-344 (OnUi: no full-screen or notification-state check); src\Ui\Controls.cs line 57 (HWND_TOPMOST on every Present)  
How to reproduce: run a PowerPoint slideshow or share the screen in Teams/Zoom while there is a problem (Level > 0), hover the widget.  
What happens / what should happen: the chip or card stays on top of the slide, and the shared screen shows it, including the hover card's "Top apps by memory" process names. Hide while SHQueryUserNotificationState is busy/presentation/D3D full screen, and set WDA_EXCLUDEFROMCAPTURE on widget and detail windows.  
Confidence: suspected (read it)  

### 31. A crafted key backup can inject lines into keys-ec.txt
Severity: security  
Where: src\Checks\EcKeys.cs lines 169-172 (Restore takes the name from the decrypted body without Safe()) and line 50 (Save writes `ec|name|...` unescaped)  
How to reproduce: restore a key backup (with the passphrase the sender supplies) whose body is `3|a\r\nec|evil|<pub>|...`; or a name containing `|`.  
What happens / what should happen: the injected `ec|...` lines become fake "My keys" entries (a known public key under a trusted name); a `|` makes the restored key vanish from the index on next read. Malformed bodies also throw ArgumentOutOfRangeException instead of a clean "damaged" message. Apply Safe(name, 60) and reject `|`, CR, LF.  
Confidence: confirmed (ran it: harness h1 on the test build reached Create(name, priv) with the injected name); the Windows Save step is suspected  

### 32. Decrypt reads the file twice: storage that changes between MAC check and decrypt feeds unauthenticated ciphertext
Severity: security  
Where: src\Checks\FileCrypt.cs lines 128-147 (Open: pass 1 HMACs the file, pass 2 seeks back to ctStart and re-reads)  
How to reproduce: open a .adeebo from an SMB/WebDAV share, a cloud placeholder or a malicious USB device that serves different bytes on the second read.  
What happens / what should happen: the second read is decrypted under the genuine key with no MAC (CBC bit-flipping) and the UI says "the file checked out". Decrypt from the bytes already MAC'd (buffer or local temp copy), or move to a chunked AEAD format.  
Confidence: suspected (read it)  

### 33. A half-decrypted file is left next to the encrypted one if the app is closed mid-decrypt; Stop does nothing
Severity: security  
Where: src\Checks\FileCrypt.cs line 250 (temp `.<guid>.opening` next to the file), line 290 (cleanup only in finally); src\Ui\FullView.Toolbox.cs line 666 (LongRunning background task)  
How to reproduce: decrypt a 1-4 GB .adeebo and close the window, log off, or kill the app part-way; or press Stop.  
What happens / what should happen: plaintext `.<guid>.opening` stays in the folder (not hidden on Windows; OneDrive may sync it); encrypt leaves a cut-off `name.adeebo` under the real name; Stop is never checked in Seal/Open. Check run.Stop, use DeleteOnClose + hidden on the temp file, write encrypt output to a temp name then rename.  
Confidence: suspected (read it)  

### 34. Decrypted file names allow bidi extension spoofing and Windows device aliases
Severity: security  
Where: src\Checks\FileCrypt.cs lines 168-180 (FreeName)  
How to reproduce: a .adeebo whose stored name is `report‮fdp.exe`, `CON .txt`, `COM¹.txt` or `CONIN$`.  
What happens / what should happen: `report‮fdp.exe` passes unchanged and shows in Explorer as "reportexe.pdf" after "Show"; `CON .txt`, `COM¹`, `CONIN$` pass and then File.Move fails with a confusing IOException. Path escape itself is blocked correctly. Strip Unicode Cf characters, check reserved names after TrimEnd(' '), add COM¹²³/LPT¹²³/CONIN$/CONOUT$, warn on executable extensions.  
Confidence: confirmed (ran it: FreeName on the test build under mono, outputs above); Windows device behaviour suspected  

### 35. "The file checked out" overstates what is checked; .p7m has no integrity at all
Severity: security  
Where: src\Checks\FileCrypt.cs lines 259 and 273 (success text), lines 277-282 (.p7m branch)  
How to reproduce: make a mode-2 .adeebo for someone using only their public key (anyone can), or flip a bit in a .p7m ciphertext, then open it.  
What happens / what should happen: both open with "the file checked out", which reads as sender authentication; EnvelopedCms (AES-CBC, no MAC) is malleable and opens silently. Say "not damaged in transit"; for .p7m say it isn't tamper-checked or sign it.  
Confidence: suspected (read it)  

### 36. speedservers.txt can point the speed test at localhost, the router or cloud metadata, and override what "auto" means
Severity: security  
Where: src\Checks\SpeedTest.cs line 59 (RemoveAll + Add moves an overridden built-in to the end), line 67 (SafeUrl), line 81 (auto = l[0])  
How to reproduce: a speedservers.txt someone hands you with id `cloudflare`, or Down/Up URLs `http://192.168.1.1/...`, `http://127.0.0.1/`, `https://169.254.169.254/...`, `http://0x7f000001/`.  
What happens / what should happen: SafeUrl returns True for all of these, so the test GETs a router admin URL with 4 streams for 8 s and POSTs 4 MB blobs to any URL; overriding `cloudflare` silently makes "auto" Vultr Toronto. The comment promises "a real host name or public address". Reject private, loopback, link-local and numeric-obfuscated hosts.  
Confidence: confirmed (ran it: SafeUrl on the test build returned True for each URL); the rest is read  

### 37. The non-ring Sniff runs dumpcap in promiscuous mode
Severity: security  
Where: src\Ui\FullView.cs line 886 (dumpcap args without -p)  
How to reproduce: Connections > Sniff with the live-capture ring off.  
What happens / what should happen: the NIC goes promiscuous, which switch port-security, NAC or IT can see, contradicting "your own PC's traffic only (no promiscuous mode)"; the ring and live paths pass -p. Add -p.  
Confidence: suspected (read it)  

### 38. Ask-the-network discovery binds UDP sockets that accept unicast replies from any LAN host for 4 s
Severity: security  
Where: src\Checks\Reach.cs lines 192-199 (u.Client.Bind(new IPEndPoint(local, 0)), mDNS QU bit set)  
How to reproduce: Toolbox > Find devices / Ask the network; Windows Firewall may ask to "allow AdeeboMeter".  
What happens / what should happen: three bound UDP ports accept inbound unicast answers while the tool runs. Short-lived and user-run, but rule 2 says "no open listening port, ever"; the owner should decide whether this counts (and the firewall prompt confuses users).  
Confidence: suspected (read it)  
Rule: 2  

### 39. Reverse lookups also send an undisclosed forward lookup, and a hung resolver thread is abandoned, not cancelled
Severity: security  
Where: src\Monitor.cs line 432 (Dns.GetHostEntry(IPAddress) in the per-network trace); src\Checks\Reach.cs lines 176 and 234 (up to 64 in parallel in Find devices)  
How to reproduce: change networks (the once-per-network trace does a PTR lookup), or run Find devices on a /24.  
What happens / what should happen: on .NET Framework GetHostEntry(IPAddress) follows the PTR with an A/AAAA lookup of the returned name, a query not listed in About; t.Wait(3000) leaves hung threads running. Use a PTR-only DnsQuery.  
Confidence: suspected (read it)  
Rule: 1  

### 40. RADIUS lab accepts an Access-Accept that has no Message-Authenticator
Severity: security  
Where: src\Checks\Protocols.cs lines 381-403 (ReplyOk checks M-A only "when the reply carries one")  
How to reproduce: a RADIUS server that answers Access-Accept with a correct Response Authenticator and no Message-Authenticator.  
What happens / what should happen: reported as a signed accept. Under current BlastRADIUS guidance a client that sent M-A should require it; show "server doesn't send Message-Authenticator - not hardened against BlastRADIUS" or treat it as unsigned. (Secret handling itself was checked and is fine.)  
Confidence: suspected (read it)  

### 41. Crash.OpenUrl allowlist uses bare prefixes
Severity: security  
Where: src\Brain\Watch.cs lines 280-285 (Crash.Allowed: StartsWith checks, PayPal added in 1.1.5)  
How to reproduce: OpenUrl("https://github.com/adeeb111/AdeeboMeterEvil/...") or "...AdeeboMeter@evil.example".  
What happens / what should happen: both pass the allowlist. Only hard-coded URLs reach it today; parse with Uri and compare host and path segment exactly.  
Confidence: suspected (read it)  

### 42. Bidi and zero-width characters in SSIDs and process metadata are drawn raw
Severity: security  
Where: src\Ui\FullView.Overview.cs line 233 (netLine), look-alike networks and Inside "Describes itself as"/"Publisher" rows  
How to reproduce: a nearby AP or exe description containing U+202E or zero-width joiners.  
What happens / what should happen: "(you)" can appear on the wrong row, or a name can look like "Microsoft Windows"; the look-alike detector compares raw strings. Strip or show Cf-category characters before display and comparison.  
Confidence: suspected (read it)  

### 43. Version is still 1.1.4 on dev: --report, the Store package and the release check all say 1.1.4
Severity: wrong result  
Where: src\Program.cs lines 66-67 (Version = "1.1.4", FileVersion = "1.1.4.0"); store\pack.ps1 lines 11-14; .github\workflows\build.yml line 83 (RELEASE-NOTES check)  
How to reproduce: run the test build with `--report out.json --seconds 5`; run pack.ps1; tag v1.1.5.  
What happens / what should happen: the JSON says `"app": "AdeeboMeter 1.1.4"`; pack.ps1 builds MSIX 1.1.4.0, which Partner Center rejects (1.1.4.0 is already submitted; the store step is continue-on-error so this is silent); a v1.1.5 tag fails the RELEASE-NOTES-1.1.5.md check; crash reports, the User-Agent, the update check and the site's `?v=` identify as 1.1.4. Bump both constants.  
Confidence: confirmed (ran it: test build under mono wrote "AdeeboMeter 1.1.4", harness read Program.Version = 1.1.4; pack.ps1 and build.yml read)  

### 44. MTR and the ISP report blame "real loss from hop N" when only the destination drops pings
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 101-131 (LossStart line 107, MtrVerdict); src\Checks\IspReport.cs lines 86-87, 94-97 and 128; asserted as correct by src\SelfTest.cs line 417  
How to reproduce: hops 192.168.1.1 0%, 10.20.0.1 0%, 203.0.113.1 0%, destination 198.51.100.7 10% (reached); or the destination at 50% (ICMP rate limiting).  
What happens / what should happen: LossStart returns the destination ("Real loss starts at hop 4 ... carries on to the end - 10% lost"), because `later` is empty and `All()` is vacuously true. The "Loss at the destination only / rate-limits pings" text and the ISP report's "only the destination drops some probes - path is clean" branch can never run at 3% or more, so the paste-ready ISP message asks the provider to "check that link". 1.1.5 fixed silent hops, but this breaks the brief's "loss counts only when it carries on". Treat destination-only loss as destination-only unless the hop before it also loses.  
Confidence: confirmed (ran it: LossStart and MtrVerdict on the test build under mono, results 3 and 2 with the text above)  

### 45. A URL typed as a target breaks every ping/trace tool and turns the VPN guide green
Severity: wrong result  
Where: src\Checks\Icmp.cs lines 100-101 (Resolve4 strips "host:port" before the URL branch); src\Checks\Investigate.cs lines 255-268 (Vpn verdict)  
How to reproduce: target `https://intranet.corp/app` in Investigate > "Works at home, not on VPN"; or `https://example.com` in mtu, mtr, ping, dscp, owners, bufferbloat (the shared box carries over from http/proxy tools).  
What happens / what should happen: the only colon is the scheme's, so the host becomes "https" and Resolve4 returns null ("couldn't find https://example.com"). In the VPN flow the port test fails (amber) but `dst == null` skips the door and MTU checks and the conclusion is green "the door opens and full packets get through". RoutingTools.RouteFor fails the same way. Handle the URL before stripping the port; never say "full packets get through" unless MtuAt ran.  
Confidence: confirmed (ran it: mono harness, Resolve4("https://192.168.1.10/app") = null, host becomes [https]; Icmp.cs and Investigate.cs unchanged in 1.1.5)  

### 46. Any non-installed copy (Run from here, portable) takes over Start with Windows, and the entry outlives Uninstall
Severity: wrong result  
Where: src\AppCtx.cs lines 40 (autoStart = true), 62, 208 and 230 (first run: ApplyAutoStart writes HKCU Run to Application.ExecutablePath); src\Setup.cs lines 410-413 ("here" drops the switch state); src\Program.cs line 130 (shared Local\AdeeboMeter.single mutex)  
How to reproduce: download the exe, switch "Start with Windows" off on the offer card, click "Just run it from here"; or run a copy with AdeeboMeter.portable next to it while the installed copy is set to autostart but not running.  
What happens / what should happen: the Run value now points at Downloads (or the zip-preview temp copy, or the portable folder), the installed copy stops autostarting, and Uninstall keeps the value because it isn't under Base, so the stray copy starts at every login after "removal". If the installed copy is running, the portable copy hands off to it via the shared mutex. RSA and P-384 keys are shared by every copy, so a portable copy can delete the installed copy's keys. Only the Store copy is exempt (AppCtx.cs line 226). Never touch the Run key from non-installed or portable copies unless asked; per-path mutex.  
Confidence: suspected (read it)  

### 47. GwEverReplied is never reset: a network whose gateway ignores ping gives a permanent "your local network is dragging"
Severity: wrong result  
Where: src\Monitor.cs line 220 (GwEverReplied only ever set), 247 (netChanged doesn't clear GwPing/GwEverReplied), 677, 686, 694 (link = 2 greys out ISP/DNS/Internet)  
How to reproduce: use the laptop at home (router answers ping), then move to an office or hotel whose gateway drops ICMP; or go offline (cable out, airplane mode).  
What happens / what should happen: after 5 NaNs the headline is "Found it: your local network is dragging (gateway - ms). The PC is innocent." with ISP, DNS and Internet greyed out, until restart, while the internet works. 1.1.5's TCP fallback only helps the Internet dot. Clear GwPing and GwEverReplied on netChanged; tell "gateway ignores ping" from "offline".  
Confidence: suspected (read it)  

### 48. Wall-clock scheduling: setting the clock back freezes probes, the sampler and now the Gamer Zone loop
Severity: wrong result  
Where: src\Monitor.cs lines 143-144 (sampler wait), 212-257 (probe intervals on DateTime.UtcNow), 684 (netStale); src\AppCtx.cs lines 269, 304, 310 (lastTop, lastCover); src\Gamer\Gamer.cs lines 385-416 (lastRun on UtcNow, lastMinute on Now)  
How to reproduce: dual-boot RTC in UTC, or set the clock back N hours (w32time correction, manual change) while AdeeboMeter runs.  
What happens / what should happen: every `(t0 - lastX)` goes negative, so no pings, Wi-Fi reads or game pings happen for N hours; netStale is negative so old pings count as "now"; lastCover in the future stops the widget folding; a jump inside a tick makes `wait` = pace + N hours and the thread sleeps for hours (widget freezes). Gamer's minute history (local Now) also stops at DST. Use Stopwatch/GetTickCount64 for intervals and clamp waits.  
Confidence: suspected (read it)  

### 49. Moving between networks that share a gateway IP raises a false ARP-spoofing alarm and a "gateway takeover" event
Severity: wrong result  
Where: src\Monitor.cs lines 415-449 (gwMacKey = adapter|gateway|SSID, MAC compared); src\Brain\Watch.cs line 38 (pop-up); src\Checks\RoutingTools.cs lines 35-46 (GwTick: lastGw/lastMac not keyed by network)  
How to reproduce: close the lid at home (192.168.1.1, SSID "NETGEAR") and open it at another place with the same gateway IP and SSID; or any two networks with the same gateway when Ssid is "" (location off, non-English netsh); or a replaced router.  
What happens / what should happen: a red "someone may be pretending to be your router (ARP spoofing)" event and a Windows pop-up, plus a contradicting amber "router failover" event and "Takeovers seen" in the Gateway view. Reset both on netChanged/resume/lease change, include DHCP server or NetKey, raise level 2 only on A-B-A flapping.  
Confidence: suspected (read it)  

### 50. "What's wrong?" says "Your router doesn't answer at all" and never tests the internet when the gateway drops ICMP and TCP
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 69-80 (concludes before step 4); src\Checks\Investigate.cs lines 86-88 (Slow: no TCP fallback); src\Ui\FullView.Replay.cs line 170 and src\Ui\FullView.Story.cs line 74 (router painted red)  
How to reproduce: a corporate L3 gateway or firewall VIP that drops ping and has no service on 53/80/443 (ASA/FTD, HSRP with an ACL, hotel and cloud gateways); run What's wrong? while the internet works.  
What happens / what should happen: "the problem is right next to you: the cable or the router itself"; Slow() says "Your router isn't answering at all" on ping alone. Still run step 4; if the internet answers, say the router just doesn't answer pings.  
Confidence: suspected (read it)  

### 51. Captive portal is blamed on the provider because step 4 returns before the sign-in-page check
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 84-94 (concludes "between your router and the internet"), sign-in check at lines 111-125 never reached  
How to reproduce: hotel or airport Wi-Fi before signing in (ICMP and TCP 443 to 1.1.1.1/8.8.8.8 dropped, HTTP 80 and DNS hijacked); run What's wrong?.  
What happens / what should happen: "restart the modem / call your provider"; the msftconnecttest probe that would find the portal never runs. Probe for a portal before concluding in step 4.  
Confidence: suspected (read it)  

### 52. Speed test grades "A+" when bufferbloat is so bad that every loaded ping times out
Severity: wrong result  
Where: src\Checks\SpeedTest.cs line 244 (Phase drops NaN pings), line 182 (Measure: NaN load becomes 0), line 203 (One: 1 s ICMP + 1 s TCP then NaN)  
How to reproduce: a line whose queue adds more than about 1 s under load.  
What happens / what should happen: every loaded ping is NaN and dropped, LoadDown/LoadUp become 0 and GradeOf(20, 0) = "A+" ("Calls and games stay smooth"); the ISP report repeats it under "ALREADY RULED OUT: the router's queue - grade A+". Partial timeouts bias the median low. Count a timeout as at least the timeout; grade "F" (or "-" with no samples).  
Confidence: confirmed (ran it: GradeOf(20,0) = A+ and GradeOf(20,1200) = F on the test build); the NaN path is read  

### 53. Global hotkey Ctrl+Alt+A swallows AltGr+A, so some keyboard layouts can't type a letter
Severity: wrong result  
Where: src\Ui\Controls.cs line 405 (RegisterHotKey Ctrl+Alt+A); the self-test only checks the key is free (src\SelfTest.cs line 789)  
How to reproduce: Polish (Programmers) layout, press AltGr+A to type "ą" in any app while AdeeboMeter runs.  
What happens / what should happen: the character never arrives and the engineer view toggles instead, system-wide, from login (autostart). It also collides with WeChat/QQ screenshot. Make the hotkey configurable with Off, default to a combination AltGr can't produce, or skip it when the layout has an AltGr character on A.  
Confidence: suspected (read it; documented Windows AltGr = Ctrl+Alt behaviour)  

### 54. Find a MAC and IP to MAC say "not in this switch's MAC table" when the table has more than 2,000 entries
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 49 (Col: walk max 2000, partial only on error); src\Checks\Snmp.cs lines 348-365 (Walk returns error "" at max); src\Checks\SnmpGear.cs lines 75-94  
How to reproduce: Find a MAC on a distribution or core switch with 5k-50k FDB entries for a MAC past the first 2,000; or IP to MAC on a router with a big ARP table.  
What happens / what should happen: "not in this switch's MAC table (2000 entries)" with advice that the switch forgot it, a confident wrong answer; no "Partial result" row. Vlans, Lag and Interfaces views truncate the same way. Report the cut at max as partial, and GET the MAC's instance directly.  
Confidence: confirmed (ran it: malicious-agent harness, Walk stops at max with error ""; Snmp.cs unchanged in 1.1.5)  

### 55. Almost all content and actions are painted and invisible to screen readers and the keyboard
Severity: wrong result  
Where: src\Ui\FullView*.cs (all DB panels with Act() hit-rects), including the new 1.1.5 pages FullView.Points.cs, FullView.Gamer.cs, FullView.ToolHome.cs and FullView.Examples.cs  
How to reproduce: run Narrator or NVDA and Tab through the engineer view; try to change any Settings value with the keyboard.  
What happens / what should happen: verdict, path dots, result cards, Settings segmented controls, About links, Overview tiles, Guide cards, Learn/Procs/Inside lists, pins and "Delete all saved history" expose no accessible objects and can't be reached with Tab/Enter; only tbList uses KeyNav. Expose acts/hints as accessible children with keyboard navigation.  
Confidence: suspected (read it)  

### 56. Offline or no-gateway states get the wrong verdict ("DNS is failing", "All quiet", "dragging")
Severity: wrong result  
Where: src\Monitor.cs line 243 (ProbeDns runs with no gateway) and lines 670-700 (Judge)  
How to reproduce: boot with the cable unplugged / Wi-Fi off / airplane mode (A); the same with the Internet check Off (B); an IPv6-only network (C).  
What happens / what should happen: A: "The internet's there but DNS lost the phone book"; B: green "All quiet. Nothing to fix - go get coffee." while the timeline says "No network connection"; C: Primary is null, so "No network connection" and no usage while DNS works. Put an explicit "not connected" verdict first; skip ProbeDns with no gateway; accept IPv6 default gateways.  
Confidence: suspected (read it)  

### 57. --report exits 0 "ok" ("the way in all work") while one of its own steps couldn't run
Severity: wrong result  
Where: src\Report.cs line 30 (lv = final Conclude level only), lines 21-22 and 37 (exit 3 also for bad arguments; other exception types rethrown)  
How to reproduce: `AdeeboMeter.exe --report existing.json --seconds 5` on a PC behind a proxy or captive portal.  
What happens / what should happen: exit 0, `"verdict": "ok"`, finding "...the internet, names and the way in all work", while observed holds `"6. The way in" ... "couldn't run (NameResolutionFailure)" level 3`; "no answer" for the processor counts as level 0. Exit 3 means both "couldn't tell" and "you called it wrong"; an unexpected exception type kills the process with the CLR code. --report ignores the user's settings (pings 1.1.1.1/8.8.8.8 even if the internet check is off) and overwrites an existing file silently. Use 3 when any step is 3 and none is 2; separate exit code for usage/IO; catch everything.  
Confidence: confirmed (ran it: test build under mono; the NameResolutionFailure comes from the sandbox but the verdict logic is platform-independent)  

### 58. ISP report: loss starting at a provider hop with a private or CGNAT address is blamed on the home network
Severity: wrong result  
Where: src\Checks\IspReport.cs line 76 (firstIsp = first non-private hop), lines 94-95, line 24 (Private)  
How to reproduce: hops 192.168.1.1 0%, 10.11.0.1 8%, 100.64.0.1 8%, 203.0.113.9 8%, destination 8%.  
What happens / what should happen: LossStart = hop 2, firstIsp = hop 4, so "Loss starts at hop 2 - before the provider (Wi-Fi or the router). Fix the home side first." and the message to the ISP changes. Many ISPs number access hops from RFC 1918 or CGNAT space; the home side ends at the gateway, so the boundary should be the hop after the gateway.  
Confidence: confirmed (ran it: LossStart = 1 on the test build); the finding text is read  

### 59. MTR: a total outage past hop N during the run is reported as "No real loss"
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 99-111 (Answers = Lost < Sent, so 100%-loss hops count as silent routers)  
How to reproduce: hops 1-4 answered the trace, then everything past hop 2 stopped answering for the whole MTR minute (Sent 120 / Lost 120 at hops 3-4, destination Reached).  
What happens / what should happen: result -1, "No real loss: ... The destination doesn't answer pings at all, so this is judged by the last router that does (hop 2)"; the ISP report then calls the path clean during an outage. A hop that answered the trace and then lost 100% (especially the reached destination) is real loss.  
Confidence: confirmed (ran it: LossStart on the test build under mono)  

### 60. Outage log: an outage that ends with a network change is never closed (merges into a 10-hour outage or stays "ongoing")
Severity: wrong result  
Where: src\Brain\Outages.cs lines 30-41 (Pair) and line 25 (60 s cache ignores days); src\Monitor.cs lines 239-240 and 245 (netChanged resets netDown without logging "back")  
How to reproduce: Wi-Fi drops ("went quiet" after 3 failures), Windows reconnects with a new address (netChanged, no "back" event). Pair input [quiet 18:00, quiet 04:00+1d, back 04:05, quiet 18:00, back 18:02].  
What happens / what should happen: "Tue 10 Nov 18:00 - 04:05 · 10 h 5 min"; with no later "back" it shows "now ... so far" forever. Same when the internet check is turned off or the app quits mid-outage; DST fall-back makes it "ongoing"; From is the 3rd failure, not the first; sleep counts as outage. Emit a closing event when netDown >= 3 is reset; close open outages at the next sample or app start.  
Confidence: confirmed (ran it: Pair on the test build); the Monitor cause is read  

### 61. pcapng nanosecond (any non-microsecond) timestamps are 1000x off in every replay
Severity: wrong result  
Where: src\Checks\Pcap.cs line 79 (`double t = ts / 1e6;`; if_tsresol in the IDB never read)  
How to reproduce: the dhcp-good sample saved as pcapng with if_tsresol = 9 (Wireshark/dumpcap default on Linux), packets 1 s apart; run the DHCP replay.  
What happens / what should happen: Offer at "1000.000 s", ACK at "3000.000 s", "Lease ... in 3000.000 s" (should be 3 s), so the timing story is wrong. Parse if_tsresol (option 9) per interface and scale.  
Confidence: confirmed (ran it: test build under mono)  

### 62. More than 20 P-384 recipients (or 256) produces a file nobody can open, including you
Severity: wrong result  
Where: src\Checks\EcKeys.cs line 106 (count byte written unchecked) vs line 129 (count > 20 -> "damaged header"); src\Checks\FileCrypt.cs lines 207-213 (no limit or de-duplication)  
How to reproduce: Encrypt for 20 saved P-384 recipients with "Also for me" = yes (21 total); or "Bob, Bob".  
What happens / what should happen: "Encrypted ... for ..." succeeds, then every recipient gets "damaged header"; 256 wraps the count to 0; duplicates are added twice. Refuse more than 20 when encrypting and de-duplicate by public key.  
Confidence: suspected (read it; mode-2 can't run on mono, the limits are exact constants)  

### 63. Store (MSIX) build: key backups, CSRs and recipients go to virtualized AppData; "Show" can't find them and uninstall deletes them
Severity: wrong result  
Where: src\Program.cs line 60 (WriteDir => Packaged ? DataDir : Dir); src\Ui\FullView.Toolbox.cs line 346 (explorer.exe /select); src\Ui\FullView.Settings.cs line 190 ("Saved in your AppData folder")  
How to reproduce: Store build, My keys > Back up private key, click Show; then uninstall the package.  
What happens / what should happen: writes are redirected to %LOCALAPPDATA%\Packages\...\LocalCache\Local\AdeeboMeter, which explorer.exe (outside the container) doesn't see, so Show selects nothing; uninstall removes LocalCache with the backups. PLAN-1.1.5 already lists "test AppData paths on a real install". Save backups through a SaveFileDialog or to Documents.  
Confidence: suspected (read it; documented MSIX behaviour, not run on Windows)  

### 64. Amber observations still end in a green "nothing is wrong" conclusion
Severity: wrong result  
Where: src\Checks\Investigate.cs line 94 (Slow concludes 0); src\Checks\Investigate.Whats.cs line 86 (thresholds)  
How to reproduce: router 16-50 ms, internet 81-150 ms, loss 1-3%, DNS 121-300 ms, signal 35-39% or bloat 21-80 ms; run Why is it slow? and What's wrong?.  
What happens / what should happen: Slow() records amber rows then says "Nothing on the path is slow right now"; What's wrong? treats 2-5% loss (robotic calls) and a 16-60 ms LAN as green, and the two flows use different thresholds; after a retest the loss isn't shown. Conclude at least 1 when any observation is amber and share one threshold set.  
Confidence: suspected (read it)  

### 65. Monitor parses English-only netsh labels and doesn't detect the location block, so Wi-Fi reads as LAN
Severity: wrong result  
Where: src\Monitor.cs lines 385-412 (ProbeWifi; `k == "Signal"` at line 397; 1.1.5 three-empty-reads hysteresis at lines 405-406)  
How to reproduce: German, Spanish or French Windows ("Status", "Kanal", "Señal", "État"); or Windows 11 24H2 with location off ("Network shell commands need location permission").  
What happens / what should happen: on localized Windows state/channel are empty so "Wi-Fi dropped/reconnected/channel changed" never fire; where "Signal" is localized HasWifi = false and a Wi-Fi laptop is "LAN"; with location off the same, and Ssid = "" (feeds the ARP false alarm). The new hysteresis only covers busy netsh and delays noticing a Wi-Fi adapter that really disappears by 3 intervals. Use the WLAN API (already in the code) and LinkInfo.Blocked().  
Confidence: suspected (read it; label strings for other languages are from memory)  

### 66. The Wi-Fi page reads only English netsh labels and then says "Your Wi-Fi is in great shape"
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 345-551 (Blocked at 345, label keys at 368-373 and 415-430, verdict at 551)  
How to reproduce: non-English Windows (German "Kanal", "Funktyp"; Spanish "Señal"), optionally with location off; open Your connection > Wi-Fi.  
What happens / what should happen: channel = -1 so neighbour analysis, width, 11k/v/r and band advice are skipped, LocationBlocked stays false, and the verdict falls through to "great shape - strong signal, clean air, quick router" built on no data. Use the Native WiFi API; never say "great shape" when signal and channel are unknown.  
Confidence: suspected (read it)  

### 67. Health check says "Your channel is calm · 0 other APs" on any non-English Windows
Severity: wrong result  
Where: src\Checks\HealthCheck.cs line 76 (regex `^\s*Channel\s*:`)  
How to reproduce: run the health check on German, French, Japanese or Turkish Windows next to a busy channel.  
What happens / what should happen: the regex finds nothing, `same = 0`, green "calm". Read the channel by position after each BSSID line or use WlanGetNetworkBssList.  
Confidence: suspected (read it)  

### 68. "Saved Wi-Fi networks" is green on non-English Windows without checking anything
Severity: wrong result  
Where: src\Checks\SecChecks.cs lines 328-337 (English regexes "Profile", "Authentication", "Connection mode")  
How to reproduce: Security checkup on German Windows ("Profil für alle Benutzer", "Authentifizierung").  
What happens / what should happen: no names match, the output still contains ':', so "0 saved · none open and auto-joining" in green. Also decoded with Encoding.Default while netsh writes OEM, so non-ASCII SSIDs fail silently. Return "couldn't read" when nothing parses; prefer WlanGetProfile XML.  
Confidence: suspected (read it)  

### 69. Esc in a text box closes the whole engineer view (now also from the new Settings search)
Severity: wrong result  
Where: src\Ui\FullView.cs lines 309-313 (KeyDown: only the Toolbox search is exempt); Settings search box at line 532  
How to reproduce: type into Check a destination, the guide box, Find a process, Learn search, the Connections filter or Settings search, press Esc to clear it.  
What happens / what should happen: the view closes and running checks, results, walk-test spots and live capture are discarded. Clear a non-empty focused text box instead and only close otherwise.  
Confidence: suspected (read it)  

### 70. Section tabs and button bars clip off the right edge at common sizes, worse in 1.1.5
Severity: wrong result  
Where: src\Ui\FullView.cs lines 141 (tabs), 216 (health bar), 240 (procs bar), 410 (9th section Gamer Zone); src\Ui\FullView.Inside.cs line 27 (chips); all FlowLayoutPanel WrapContents = false  
How to reproduce: Pro mode, 1366x768 at 125% (or 1920x1080 at 175%), French captions.  
What happens / what should happen: with 9 sections, the Points chip, the wider SlideSwitch and longer French text, "Time machine" and "Learn" are cut off or missing (reachable only via Ctrl+K); Inside chips clip even at 100%. Wrap or shorten (FitTabs) and add overflow.  
Confidence: suspected (read it; widths estimated from font sizes)  

### 71. Traffic Lab packet builders and scan-style tools earn Adeebo Points
Severity: wrong result  
Where: src\Points.cs lines 20-23 (NoEarn and Big lists); src\Ui\FullView.Toolbox.cs line 203 (LabGate)  
How to reproduce: open the Example for ICMP builder, ARP who-has, DHCP options, Many ports, Find devices or Ping sweep, tick "my own network", Run.  
What happens / what should happen: Points.Value gives icmpb 1, arpwho 1, dhcpopt 1, manyports 1, sweep 1, finddev 2, despite "the Traffic Lab senders never earn"; points nudge users to tick "I'm allowed" on a company network. The replay bonus also rewards running speedtest, ispreport, iperf and bloat twice. Derive NoEarn from LabGate plus sweep; drop replay for data-heavy tests.  
Confidence: confirmed (ran it: Points.Value on the test build under mono)  
Rule: 10  

### 72. A failed read of points.txt at startup wipes the saved total on the next award
Severity: wrong result  
Where: src\Points.cs lines 41-58 (Load swallows IO/Unauthorized errors and leaves the map empty) and lines 59-65 (Save rewrites from the map)  
How to reproduce: points.txt is locked when Points.Me is first touched (antivirus scan, OneDrive, backup tool), then earn any point.  
What happens / what should happen: the file is replaced with only the new entry; the whole history and total are gone, breaking "the total survives a restart". Remember the failed load and refuse to save (or merge with the file on disk).  
Confidence: suspected (read it; the lock couldn't be reproduced on Linux as root)  
Rule: 10  

### 73. The "+2 replay, 10 or more minutes later" rule can be beaten by moving the clock
Severity: wrong result  
Where: src\Points.cs line 83 (`nowUtc - e.First < ReplayGap` with DateTime.UtcNow from src\Ui\FullView.Points.cs line 86)  
How to reproduce: run MTR, move the Windows clock forward 10 minutes, run MTR again.  
What happens / what should happen: +2 at once. A clock moved back stores a future First and the replay never pays. Low impact (points only cheat yourself); use a session Stopwatch and require First <= now.  
Confidence: confirmed (ran it: Award on the test build: first 2, "+11 min" 2, "+20 min" 0)  
Rule: 10  

### 74. "Use a page for the first time" pays just for opening the tab
Severity: wrong result  
Where: src\Ui\FullView.Points.cs line 103 (PageUsed called from every ShowTab2)  
How to reproduce: from the Points dashboard click Go on each "use a page" item without doing anything on the page.  
What happens / what should happen: +1 per page opened, so 10 clicks give +10 without using anything; Gamer and Time machine correctly require real use. Award on a real action.  
Confidence: suspected (read it)  
Rule: 10  

### 75. Self-test writes fake events into the user's real history
Severity: wrong result  
Where: src\SelfTest.cs lines 780-783 (Monitor.SimulateForTest) -> src\Model.cs line 116 (Timeline.Add) -> History.Event  
How to reproduce: run `AdeeboMeter.exe --selftest` (build_exe.cmd does it after every build), then open History/Changes for today.  
What happens / what should happen: "VPN connected (Example VPN Client Adapter)", "Switched to LAN", "Wi-Fi roamed ... aa:bb:cc:00:00:02", "Wi-Fi disconnected", "IP address changed" appear as if real. Made-up data is saved as a real run. Turn History and Alerts off for the self-test.  
Confidence: suspected (read it)  
Rule: 9  

### 76. Example inputs point at real third-party and LAN hosts, not documentation ranges
Severity: wrong result  
Where: knowledge\examples.txt lines 70, 116, 136, 141, 165, 190, 216, 290, 302, 307, 355-422; header and src\Examples.cs line 13 promise "documentation ones only"  
How to reproduce: open the Example for ping, any SNMP tool, ntp, dnscmp, proxy, speedtest or gamecompare, click "Fill the boxes", Run.  
What happens / what should happen: SNMP with community "public" to 192.168.1.2 / .30 / .5 (real devices on many home LANs), 50 pings at 200 ms to 192.168.1.1, NTP to time.windows.com, DNS to 1.1.1.1, a real speed test, pings to Riot servers, route to 10.20.30.40, nbname/shares to 192.168.1.40 and nas01, and a real TP-Link OUI in wol. Use 192.0.2.x / 198.51.100.x / 203.0.113.x / example.com; leave in: out where a real target is the point.  
Confidence: confirmed (ran it: parsed examples.txt from source and the test build's embedded copy)  
Rule: 9  

### 77. "Describe it in your own words" sends ordinary sentences to the wrong guide
Severity: wrong result  
Where: src\Ui\FullView.Overview.cs line 124 (FixWords) and line 147 (FixMatch: " " + word prefix match, rule "vpn|work|office|company" first)  
How to reproduce: type "my internet doesn't work", "printer not working", "my hotspot doesn't work", "Wi-Fi works but nothing loads", "my VPN won't connect", "ping is high".  
What happens / what should happen: the first four go to the VPN guide ("works at home but not on VPN or at work"), "won't connect" goes to "the VPN may be the slow part", "ping is high" to Gamer. " work" matches works/working; the second rule's vpn can never match. Match whole words, drop bare "work", put "no internet / not working" first.  
Confidence: confirmed (ran it: FixMatch on the test build under mono)  

### 78. THE PC tools have unreachable examples, and Go in the dashboard runs them (a whole-disk scan) at once
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs lines 209-214 (OpenTool sends health tools to RunTool); src\Ui\FullView.Points.cs line 63 (`if (sec != "health") ShowExample();`); src\Ui\FullView.Tools.cs line 31 (earns with opened: true)  
How to reproduce: Points dashboard > "Where did my space go?" > Go; or open any THE PC tool from search or Recent.  
What happens / what should happen: the tool runs immediately; its example (tidy, pressure, disk, space, bg, crash, leaks, gpu, changed, battery: 10 examples) can never be seen, and these tools earn without an Example unlike every other tool. Go should open the tool with its Example, not run it.  
Confidence: suspected (read it; deterministic path)  

### 79. Adding two games whose names reduce to the same id silently replaces the first
Severity: wrong result  
Where: src\Ui\GameEditor.cs line 104 (Id = "my-" + letters/digits); src\Gamer\Gamer.cs lines 121-131 (SaveGame drops every line with that id)  
How to reproduce: add "Fortnite Duo", then "fortnite-duo!" (or "CS 2" and "CS-2", or "!!!" and "???", both my-game).  
What happens / what should happen: the first game and its servers are gone; games.txt holds only `my-fortniteduo|fortnite-duo!`. Suffix the id or refuse with "You already have a game called ...".  
Confidence: confirmed (ran it: SaveGame twice on the test build under mono)  

### 80. GameEditor Save does nothing when the program box holds a quoted path
Severity: wrong result  
Where: src\Ui\GameEditor.cs line 102 (Path.GetFileNameWithoutExtension before the invalid-character filter)  
How to reproduce: paste `"C:\Games\VALORANT.exe"` (Explorer's Copy as path, with quotes) or text with `<`, `>`, `|`, press Save.  
What happens / what should happen: on .NET Framework GetFileNameWithoutExtension throws ArgumentException, the click handler dies (logged) and the dialog just sits there. Trim quotes, filter first, wrap in try/catch.  
Confidence: suspected (read it; mono doesn't throw here)  

### 81. VPN awareness misses Tailscale, WARP, NordLynx, ZeroTier and Wintun, calls some full tunnels "split", and calls PPPoE a VPN
Severity: wrong result  
Where: src\Monitor.cs lines 269-276 (vpnWords, IsVpn); src\Checks\VpnAware.cs lines 46-54 (FullTunnel = adapter has an IPv4 gateway)  
How to reproduce: connect Tailscale / Cloudflare WARP / NordLynx / ZeroTier / a Wintun tunnel; WireGuard full tunnel (0/0 on-link) or OpenVPN def1; a PPPoE connection on the PC.  
What happens / what should happen: "Tunnel adapter: none up - the VPN isn't connected right now" and the tunnel layer is skipped; full tunnels without an adapter gateway read "company traffic only (split tunnel)"; PPPoE shows as a VPN with the ISP DNS as "the VPN's far side". Extend the words, check the routing table for default/half-default routes via the tunnel, exclude PPPoE.  
Confidence: suspected (read it; adapter descriptions from those products' drivers)  

### 82. "Find my match server" picks the PC's own address when the PC has a public IPv4
Severity: wrong result  
Where: src\Gamer\Gamer.cs lines 652-664 (MatchFinder.Busiest)  
How to reproduce: tshark output where the PC's own public address (bridge-mode modem, university network) appears in every packet.  
What happens / what should happen: Busiest returned 203.0.113.50 (the PC) instead of the match server 198.51.100.20; it is added as the "exact" server (src\Ui\FullView.Gamer.cs line 156) and pinged. Exclude the PC's own addresses or count the remote side per direction.  
Confidence: confirmed (ran it: test build under mono)  

### 83. Speed test measures redirects and other non-file answers as a real speed
Severity: wrong result  
Where: src\Checks\SpeedTest.cs line 146 (AllowAutoRedirect = false), lines 232-236 (download loop), line 139 (Head)  
How to reproduce: a server line or URL whose server answers 302 (moved test file, http->https upgrade).  
What happens / what should happen: GetResponse returns the 302 without throwing, the loop re-fetches a few hundred bytes for 8 s and reports e.g. "Download 0.2 Mbps" in green, saved as SpeedTest.Last, to History and the ISP report; `check` calls the server "answers". Treat anything but 200/206 (or a tiny Content-Length) as "didn't send its test file".  
Confidence: suspected (read it)  

### 84. Speed test: only 2 of the 4 streams run at once, and the time base includes join waits
Severity: wrong result  
Where: src\Checks\SpeedTest.cs lines 215-246 (no ServicePointManager connection limit set anywhere; secs taken after `t.Join(3000)` at lines 245-246)  
How to reproduce: run a speed test on a fast line.  
What happens / what should happen: .NET Framework desktop apps get 2 connections per host, so streams 3-4 queue and can time out after 6 s (counted as fails); a stuck stream stretches the divisor by up to 3 s per thread. Both understate fast lines. Raise the connection limit to the stream count; record secs when done() first turns true.  
Confidence: suspected (read it; documented framework behaviour)  

### 85. Speed test stopped during upload is saved and reported as a complete result
Severity: wrong result  
Where: src\Checks\SpeedTest.cs lines 172-186 (run.Stop checked only after download); line 108 (NaN upload shown green)  
How to reproduce: start a speed test and press Stop during the upload phase.  
What happens / what should happen: Up is computed from the partial window, graded, saved as SpeedTest.Last and to History ("X down / Y up"); with no upload samples a green "Upload - Mbps" row appears. Mark it stopped after the upload too.  
Confidence: suspected (read it)  

### 86. SMTP path shows an invalid certificate as "... > certificate checked" in green; the port-25 hint is dead code
Severity: wrong result  
Where: src\Checks\EngPack.cs lines 300-310 (HowFar: substring search over rows), lines 353-354  
How to reproduce: a server whose STARTTLS certificate fails validation ("The remote certificate is invalid according to the validation procedure.").  
What happens / what should happen: HowFar returns "MX found > connected > greeting > EHLO > STARTTLS > certificate checked" and line 353 makes it level 0; server banner words also steer it. Line 354 tests for "couldn't", which HowFar never produces, so the "providers block outgoing port 25" advice never shows. Build the steps from structured results.  
Confidence: confirmed (ran it: HowFar on the test build); line 354 is read  

### 87. Certificate chain compare never compares sites on a port other than 443
Severity: wrong result  
Where: src\Checks\EngPack.cs line 41 (Host = h + ":" + port) vs line 33 (ParseCert requires Games.ValidHost(p[1]))  
How to reproduce: run the chain compare twice for `intranet.example.com:8443`.  
What happens / what should happen: ParseCert("snap|intranet.example.com:8443|...") returns null, so every saved snapshot is dropped on reload and the second run always says "now change one thing and run again"; those lines are also lost from cert-compare.txt. Validate host and port separately.  
Confidence: confirmed (ran it: ParseCert on the test build returned null)  

### 88. RADIUS lab: a User-Password over 128 bytes produces a malformed packet
Severity: wrong result  
Where: src\Checks\EngPack.cs lines 199-211 (HidePassword: copy clamped to 128 but n isn't) and line 280 (add: length byte wraps)  
How to reproduce: RADIUS lab with a 129-260 byte password.  
What happens / what should happen: 252 bytes -> attribute length byte 2; 260 -> 18; 129-200 -> 144-208 bytes, past RFC 2865's 128 and with encrypted zeros after byte 128. The server drops it and the tool says "wrong shared secret / not a client". Refuse passwords over 128 bytes.  
Confidence: confirmed (ran it: HidePassword on the test build under mono)  

### 89. "Slower than usual" fires right after switching networks
Severity: wrong result  
Where: src\Brain\Brain.cs lines 193-212 (recentGw/recentNet shared by every network)  
How to reproduce: tether on a phone hotspot (gateway about 40 ms), then get home where the learned baseline is 2 ms with N >= 120.  
What happens / what should happen: the first home minutes average hotspot values: "Slower than usual on Home - the link to your router is 20x slower than normal" plus a pop-up. Keep the recent window per network key.  
Confidence: suspected (read it)  

### 90. History compaction can overwrite a day's .gz, and Lines ignores the .gz when a .csv exists (now also the outage log)
Severity: wrong result  
Where: src\History.cs line 68 (Tidy: File.Create(f + ".gz") truncates), line 95 (Lines reads only the .csv if both exist), line 109 (EventsOf, used by src\Brain\Outages.cs)  
How to reproduce: travel west across midnight, or set the clock back, so lines are appended to yesterday after it was compressed; open History, then wait for the next Tidy.  
What happens / what should happen: History shows only the new fragment, then Tidy replaces the whole compressed day with it; the 1.1.5 outage log loses that day's outages too. Merge into the existing .gz; read both in Lines.  
Confidence: suspected (read it)  

### 91. Alt+F4 during install or uninstall leaves a half-done install
Severity: wrong result  
Where: src\Setup.cs line 275 (only Esc is blocked while Working) and line 436 (work on a background task); src\Program.cs lines 122-124 (Main returns when the window closes)  
How to reproduce: press Alt+F4 (or taskbar "Close window") during the ~2 s install.  
What happens / what should happen: the background thread dies mid-way: exe copied without Uninstall key or shortcut, old versions deleted without a new entry, or on uninstall the app killed but nothing removed. Cancel FormClosing while Working, or run on a foreground thread and Join.  
Confidence: suspected (read it)  

### 92. A plain exe in a read-only folder shows the install offer on every launch and never saves settings (partly fixed)
Severity: wrong result  
Where: src\Setup.cs lines 54-55 (ShouldOffer: no settings file next to the exe); src\Program.cs line 60 (WriteDir => Packaged ? DataDir : Dir)  
How to reproduce: copy the exe to C:\Program Files\AdeeboMeter\ or \\server\tools (or a portable copy on a read-only stick), choose "Just run it from here", restart.  
What happens / what should happen: 1.1.5 fixed this only for the Store copy. Elsewhere SaveSettings and the log fail silently, the offer card returns on every launch including the Run-key start at login, and Settings changes are lost. Fall back to DataDir when Program.Dir isn't writable.  
Confidence: suspected (read it)  

### 93. Running an older exe offers "Update" and deletes the newer installed version
Severity: wrong result  
Where: src\Setup.cs lines 280-281 (Upgrade = instVer != Program.Version) and line 341 (button text)  
How to reproduce: 1.2.0 installed, double-click an old 1.1.4 exe left in Downloads.  
What happens / what should happen: "Update to 1.1.4 - You have 1.2.0", then "Tidying up older versions" deletes 1.2.0; settings keys 1.1.4 doesn't know may be lost. Compare with System.Version and offer to open the newer one.  
Confidence: suspected (read it)  

### 94. Renamed copies escape StopOthers/KillOthers; a "run from here" copy can't be updated by a newer download
Severity: wrong result  
Where: src\Setup.cs line 94 (GetProcessesByName("AdeeboMeter")); src\Program.cs Single (AskRunningToOpen, KillOthers)  
How to reproduce: a "run from here" copy in Downloads (settings file exists); the browser saves the next release as `AdeeboMeter (1).exe`; double-click it.  
What happens / what should happen: no update card (ShouldOffer is false), the old copy holds the mutex and opens its view, the new version exits, and the user believes they updated. A hung `AdeeboMeter (1)` process can't be killed by name, so the mutex wait times out and the app returns 0 silently. Find copies by mutex owner or image path and hand over to the newer version.  
Confidence: suspected (read it)  

### 95. Writers and readers lock different objects (Calls, Devices, Pins)
Severity: wrong result  
Where: src\Brain\Watch.cs lines 57 and 85-86 (Summaries under lock(k)), 205 and 223-224 (Joined under Devices.k); readers src\Ui\FullView.Overview.cs line 345, src\Checks\Investigate.cs line 159, src\Ui\FullView.Insights.cs lines 68-75 (no lock), src\Checks\SecChecks.cs line 467; Cfg.Pins src\Program.cs line 221 vs src\Brain\Watch.cs lines 245-248 (sampler thread)  
How to reproduce: a call ends or the 10-minute device check adds a row while History/Insights paints; remove a pin while Pinned.Tick runs.  
What happens / what should happen: "Collection was modified" or ArgumentOutOfRange in paint (logged as "UI error", a card missing that frame). Lock on the list itself in writers or publish immutable snapshots.  
Confidence: suspected (read it; rare race)  

### 96. SNMP walk accepts OIDs that go backwards or loop: duplicate data reported as complete, or the device hammered for 2 minutes
Severity: wrong result  
Where: src\Checks\Snmp.cs lines 355-361 (Walk only rejects equality with cur)  
How to reproduce: an agent that always answers `.10.1, .10.2`, or one that answers an earlier OID each time (buggy printers, old Cisco agents).  
What happens / what should happen: 1000 requests and 2000 "values" from 2 distinct OIDs with error "" (a clean finish); or 1460 requests over 3 OIDs until the deadline. Compare OIDs numerically and stop with "OID not increasing"; keep a seen-set.  
Confidence: confirmed (ran it: malicious SNMP agent against the real Snmp.Walk under mono; Snmp.cs unchanged in 1.1.5)  

### 97. SNMP Traffic on 32-bit counters turns a reboot or counter reset into a huge phantom rate
Severity: wrong result  
Where: src\Checks\SnmpTools.cs lines 168-172 (any decrease taken as a wrap; no sysUpTime read)  
How to reproduce: Traffic on a device without HC counters that reboots or resets a counter between samples (p = 4.0e9, q = 1000).  
What happens / what should happen: 294,968,296 bytes = 471.9 Mb/s over 5 s, 472% "busiest" on a 100M port, marked as trouble; a non-conforming value above 2^32 underflows to 1.8e19. Read sysUpTime before and after, accept a wrap only when the implied rate fits ifSpeed, else show "reset".  
Confidence: confirmed (ran it: Traffic wrap maths in the mono harness)  

### 98. IPv6 vs IPv4: an IPv6-only site, or broken IPv4, is reported as "Neither connected." in green
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 211-214  
How to reproduce: v4 = none, v6 = 20 ms; or both failing.  
What happens / what should happen: "Neither connected." at level 0 in all of these. Enumerate the four combinations; level 2 for neither.  
Confidence: confirmed (ran it: mono harness on the same code)  

### 99. Routing listen counts any DHCP client as a second DHCP server
Severity: wrong result  
Where: src\Checks\RoutingTools.cs lines 160-161 (sources from IP fields) and line 226 (lv 1 when sources > 1)  
How to reproduce: run the routing listen on a busy LAN where a client renews (DISCOVER/REQUEST from 0.0.0.0, broadcast INFORM).  
What happens / what should happen: amber "More than one DHCP server answers on this network" on almost every LAN. Count distinct dhcp_server_id only on OFFER and ACK.  
Confidence: suspected (read it)  

### 100. "What's wrong?" fails the DNS step when Windows uses DoH or only the second DNS server works
Severity: wrong result  
Where: src\Checks\Investigate.Whats.cs lines 98-109 (one UDP/53 query to the first IPv4 DNS)  
How to reproduce: Windows 11 DoH-only, a split-tunnel VPN DNS that refuses public recursion, or a dead primary with a working secondary.  
What happens / what should happen: "names don't turn into addresses - so every site and app fails" while apps resolve fine. Cross-check with the OS resolver and try every server.  
Confidence: suspected (read it)  

### 101. Averaged, untimestamped ring data hides a link that is down right now
Severity: wrong result  
Where: src\Model.cs line 47 (Ring.Avg skips NaN); used by Investigate.Whats (GwPing.Avg(6), NetPing.Avg(6))  
How to reproduce: 5 of the last 6 pings failed; or samples minutes old after sleep/resume or a slow NetSec.  
What happens / what should happen: the average of the one success is reported and not retested, despite "only what's stale is re-tested". Keep sample times; retest when the newest is NaN or older than 10 s.  
Confidence: suspected (read it)  

### 102. MtuAt calls ICMP rate limiting an "MTU black hole"
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 44-62 (two silent probes at one size = too big)  
How to reproduce: a path where routers send about 1 TTL-exceeded per second; run the MTU tool or the VPN guide.  
What happens / what should happen: a lower MTU and BlackHole = true, and the VPN guide concludes level 2 "MTU black hole ... tell IT this number". Re-probe a working size and the failing size after 1 s before concluding.  
Confidence: suspected (read it)  

### 103. WinEvidence counts 802.1X success events as failed sign-ins
Severity: wrong result  
Where: src\Checks\WinEvidence.cs line 93 (WLAN-AutoConfig 12012, Wired-AutoConfig 15501)  
How to reproduce: a PC with normal 802.1X logons; open the sign-in evidence.  
What happens / what should happen: each success shows as a red failure row (failures are 12013 and 15502/15514, to be checked on a real system).  
Confidence: suspected (read it; event IDs from memory)  

### 104. Look-alike ("evil twin") warning uses the 3-byte OUI and raises false alarms on enterprise and mesh networks
Severity: wrong result  
Where: src\Link\WifiAdvisor.cs lines 113-121  
How to reproduce: a campus SSID served by APs bought in several batches (different OUIs), or your own AP using a locally-administered BSSID.  
What happens / what should happen: genuine APs are flagged "a different maker's hardware ... Don't sign in to anything on it." Skip when MyBssid is LAA, compare vendor names or security/RSN, soften the wording.  
Confidence: suspected (read it)  

### 105. Live capture keeps saying "capturing" after tshark dies
Severity: wrong result  
Where: src\Capture\LiveCapture.cs lines 146-153 (ReadLine returns null while running is true, Stop never queued); ring.BeginErrorReadLine at line 116 has no handler  
How to reproduce: start Live capture, then sleep/resume, reset the adapter or drop Wi-Fi.  
What happens / what should happen: "capturing 47:12 of 15:00" with zero packets forever (the 15-minute limit is only checked when a line arrives); ring errors (disk full, adapter gone) are discarded and Sniff silently switches path. Detect tshark exit and set an error.  
Confidence: suspected (read it)  

### 106. The policy permanently overwrites the user's own settings
Severity: wrong result  
Where: src\Policy.cs lines 68-74 (Apply writes Cfg.GameIds = "", Cfg.NetSec = 0, HistoryDays, Mode); src\AppCtx.cs lines 206-208 (next SaveSettings persists them)  
How to reproduce: run once under a policy with game monitoring off / internet check off, then remove the policy.  
What happens / what should happen: the user's game list and internet-check choice are gone for good (also after removing a planted policy). Enforce at read time, as Monitor.cs line 212 does for NoInternetCheck, and never write policy values into Cfg.  
Confidence: suspected (read it)  

### 107. P-384 keys on a portable/USB copy: the index travels, the key doesn't
Severity: wrong result  
Where: src\Checks\FileCrypt.cs line 213 (EcKeys.All().FirstOrDefault(), no CngKey.Exists check); src\Checks\EcKeys.cs line 135; src\Ui\FullView.Toolbox.cs line 273 (Delete)  
How to reproduce: portable copy on a USB stick, make a P-384 key on PC A, run the stick on PC B.  
What happens / what should happen: PC B lists the key, but opening files for it fails ("wasn't made for any of your keys"), "Also for me" encrypts to a key not on this PC, and Delete on PC B orphans the real key on PC A. Mark keys whose CNG key is missing as "not on this PC" and skip them.  
Confidence: suspected (read it; needs CNG)  

### 108. Encrypting for P-384 recipients with "Also for me" and no P-384 key of your own gives no warning
Severity: wrong result  
Where: src\Checks\FileCrypt.cs line 213 (EC branch: me == null silently adds nothing) vs line 230 (RSA branch warns); src\Checks\CertKeys.cs lines 274 and 284  
How to reproduce: have only an RSA key, encrypt for P-384 recipients with Also for me = yes.  
What happens / what should happen: you can't open your own file and aren't told. Also: Bob.cer and Bob.adeebokey together - P-384 always wins; re-adding a recipient under the same name silently replaces the key (a silent key change). Warn in both cases.  
Confidence: suspected (read it)  

### 109. Import settings wipes pinned destinations before validating the file; widget and theme are counted but ignored
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 516-522 (Cfg.Pins.Clear() first, then Cfg.Save()); src\Program.cs (Cfg.Read has no widget/theme case)  
How to reproduce: Settings > Import, pick a .txt or an older export without pin= lines.  
What happens / what should happen: pins are cleared and saved, page says "Loaded 0 settings."; widget/theme are reported loaded but never applied. Parse first, replace pins only if the file had known keys, apply or don't count widget/theme.  
Confidence: suspected (read it)  

### 110. "Delete all saved history now" deletes on one click with no confirmation
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs lines 232-237  
How to reproduce: one stray click on the red link under the Settings rows (220x22 hit area).  
What happens / what should happen: up to 90 days of history is deleted permanently, no undo. Confirm (Yes/No, default No) or click-again-within-3-s.  
Confidence: suspected (read it)  

### 111. System-aware DPI only: blurry widget and wrong cover detection on mixed-DPI monitors
Severity: wrong result  
Where: src\Program.cs line 83 (SetProcessDPIAware); src\AppCtx.cs WindowOver (DWMWA_EXTENDED_FRAME_BOUNDS, physical) vs Zone() (virtualized)  
How to reproduce: a second monitor at a different scale, or change the scale without signing out; put a window over the widget corner.  
What happens / what should happen: DWM stretches the UI (blurry for days, the app autostarts) and windows are judged to cover or not cover the corner incorrectly, so the card stays open over a window or folds over nothing. Opt in to PerMonitorV2, or use GetWindowRect consistently.  
Confidence: suspected (read it)  

### 112. Cover detection uses a fixed 264x300 zone, so bigger or taller cards aren't folded
Severity: wrong result  
Where: src\AppCtx.cs line 293 (w = 264*S, h = 300*S)  
How to reproduce: Bigger widget on (card 343*S wide), or a Teams call strip with a two-line headline; put a window over the card's upper-left part.  
What happens / what should happen: the window isn't detected and the topmost card stays over its content; the near fast-poll zone is wrong too. Derive the zone from widget.Placed.  
Confidence: suspected (read it)  

### 113. The Connections list doesn't scroll with precision touchpads or hi-res wheels
Severity: wrong result  
Where: src\Ui\Controls.cs line 393 (`e.Delta / 120 * RowH * 3`, integer division)  
How to reproduce: two-finger scroll over Connections on a modern laptop (deltas 8-40).  
What happens / what should happen: e.Delta / 120 = 0, nothing scrolls. Multiply before dividing or accumulate the remainder.  
Confidence: suspected (read it)  

### 114. Holding Enter or Space on a toggle repeats it (live capture starts and stops tshark repeatedly)
Severity: wrong result  
Where: src\Ui\Controls.cs line 125 (PillButton.OnKeyDown), line 427 (new SlideSwitch), line 455 (ThemeSwitch)  
How to reproduce: focus "Start live capture", "Pin" or the Details SlideSwitch and hold Enter.  
What happens / what should happen: Click fires on every auto-repeat, so Live capture starts and kills tshark many times and Details flips on and off. Act on KeyUp or ignore repeats.  
Confidence: suspected (read it)  

### 115. A second launch opens the view behind other windows
Severity: wrong result  
Where: src\AppCtx.cs line 253 (full.Activate() from BeginInvoke in the background instance)  
How to reproduce: with AdeeboMeter running, double-click the exe or shortcut again.  
What happens / what should happen: usually only a flashing taskbar button, so the user thinks nothing happened. Call AllowSetForegroundWindow(ASFW_ANY) in the second instance before signalling.  
Confidence: suspected (read it)  

### 116. Toolbox and dashboard search ignore the language the user sees
Severity: wrong result  
Where: src\Ui\FullView.Toolbox.cs lines 709-734 (Matches on English t.Title and Explain); src\Ui\FullView.Points.cs lines 74-80 (English Title, Tag, Group)  
How to reproduce: Français, Toolbox search "pare-feu", "débit", "fuite" or "vitesse".  
What happens / what should happen: nothing matches and "Did you mean" suggests English words. Also match L.T(title) and translated tags; translate the "Nothing matches / Did you mean" lines.  
Confidence: suspected (read it; deterministic)  

### 117. Deep test contacts the company domain controller despite "never your company's servers"
Severity: wrong result  
Where: src\Ui\FullView.DeepTest.cs line 24 (ByHand list lacks ldapcheck and shares); src\Checks\MoreTools.cs line 313 (Ldap("") falls back to %USERDNSDOMAIN%)  
How to reproduce: run `--deeptest` on a domain-joined PC.  
What happens / what should happen: section 1 feeds "" and " " into ldapcheck, so an LDAP rootDSE query and TCP knocks on 636/3268/88 go to the company DC twice; hostile names (999.999.999.999) go through the company DNS and NetShareEnum triggers NetBIOS/LLMNR broadcasts. Add ldapcheck and shares to ByHand or pre-fill 127.0.0.1.  
Confidence: suspected (read it)  

### 118. "HTTP and TLS inspector" says TLS 1.3 "not offered" when it's this PC that can't
Severity: wrong result  
Where: src\Checks\Toolkit.cs lines 645-656 (TLS 1.3 handshake attempt, also via the new SNI parameter)  
How to reproduce: run the inspector on Windows 10 (SChannel has no TLS 1.3 client) against any modern site.  
What happens / what should happen: every site is "TLS 1.3 not offered". DestCheck.Tls13Client() already knows; say "this PC can't test TLS 1.3".  
Confidence: suspected (read it)  

### 119. dig silently falls back to the system DNS when the server box isn't an IP
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 369  
How to reproduce: dig with server `dns.google`, `8.8.8.8:53`, or a typo `1.1.1.l`.  
What happens / what should happen: the query goes to the system resolver and the Status row says "from 192.168.1.1", easy to miss; the user thinks they compared resolvers. Error out (or resolve the name and say so).  
Confidence: suspected (read it)  

### 120. IDN names are sent to DNS as '?'
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 442 (Question: Encoding.ASCII); src\Checks\Reach.cs line 125 (DnsQ); src\Checks\MoreTools.cs line 125 (DnsCompare regex lets them in)  
How to reproduce: dig or DNS compare for `bücher.de`.  
What happens / what should happen: the wire label is `b?cher`, reported as NXDOMAIN "the name doesn't exist". DestCheck accepts `https://bücher.de` via IdnHost but not the bare name. Use IdnMapping.GetAscii.  
Confidence: confirmed (ran it: mono harness, wire label b?cher)  

### 121. "Hash a file and compare" says DOESN'T MATCH for common paste formats
Severity: wrong result  
Where: src\Checks\CertTools.cs line 128 (keeps every hex digit in the pasted text)  
How to reproduce: paste `SHA256: e3b0...` or certutil's `SHA256 hash of f.txt: e3b0...` as the expected value for a matching file.  
What happens / what should happen: the letters of the words (A, 2, 5, 6, a, f...) are kept, so red "DOESN'T MATCH ... The file is damaged". Extract 32-128-hex-character tokens and compare each.  
Confidence: confirmed (ran it: mono harness with an empty file and its correct SHA-256)  

### 122. Subnet calculator turns bad prefixes into /32 silently
Severity: wrong result  
Where: src\Checks\Toolkit.cs lines 789-795  
How to reproduce: `10.0.0.0/-1`, `10.0.0.0/abc`, `10.0.0.0/8 foo`; `10.0.0.1 255.0.255.0`; `10.0.0.1 ffff::`; `1.2.3/24`.  
What happens / what should happen: "10.0.0.0/32 (one host)"; non-contiguous mask -> /8; IPv6 mask on IPv4 -> /16; 1.2.3/24 read as 1.2.0.3. Default to /32 only with no prefix; error otherwise.  
Confidence: confirmed (ran it: mono harness)  

### 123. A scoped IPv6 literal with a port loses the port
Severity: wrong result  
Where: src\Checks\DestCheck.cs lines 70-73 (m6 regex has no %)  
How to reproduce: Check a destination `[fe80::1%12]:8443`.  
What happens / what should happen: the check runs against port 443. Allow `%\w+` in the regex.  
Confidence: confirmed (ran it: mono harness, port 443)  

### 124. Smaller parsing slips in the input tools
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 553 and src\Checks\Protocols.cs line 66 (StartsWith("http")); src\Checks\Protocols.cs line 31 (Split); src\Checks\CertTools.cs line 283 (JWT exp); src\Checks\MoreTools.cs lines 164, 207, 263; src\Checks\Reach.cs line 34; src\Checks\Toolkit.cs lines 418-424  
How to reproduce: http tools on `httpbin.org`; `host:abc` in ssh/mail/ntp; a JWT with a 13-digit exp; ManyPorts `100000`; MySites with an empty address list; Rdp to an unreachable network; HTTP builder header `Content-Type: application/json; charset=utf-8`; dig whose TCP fallback is refused.  
What happens / what should happen: hosts starting with "http" are "not a web address"; a bad port silently uses the default; exp ms reads "Expires 2518-08-29"; 100000 becomes port 10000; MySites throws IndexOutOfRange ("Couldn't finish"); any connect error is "refused - the PC is there"; headers are cut at ';' without being listed in "Not sent"; GET + body drops the body silently; AggregateException/IOException escape Query ("Couldn't finish"). Also DestCheck accepts `1.2.3`, `0x7f.1`, `4294967295` as IPs and Ldap misses level 10 and stops at the first 0x65 byte.  
Confidence: confirmed (ran it: mono harness for the parsing cases); the rest is read  

### 125. Mail, Revocation, Tls and X509Days use IPv4-only TcpClient
Severity: wrong result  
Where: src\Checks\Toolkit.cs line 650 and the other `new TcpClient()` + ConnectAsync(host) callers  
How to reproduce: target an IPv6-only host or `[::1]`.  
What happens / what should happen: an address-family error shown as "not offered" / "couldn't connect"; Toolkit.Tls passes `[::1]` with brackets as a DNS name. Use dual-mode sockets and strip brackets.  
Confidence: suspected (read it)  

### 126. BitLocker protection values may be mapped wrongly
Severity: wrong result  
Where: src\Checks\SecChecks.cs lines 106-111 (BitLocker: 5 = on, 6 = off)  
How to reproduce: suspend BitLocker (`manage-bde -protectors -disable C:`), or a locked data volume; run Security checkup.  
What happens / what should happen: commonly documented values are 5 = suspended and 6 = on (locked), so a suspended drive would show green and a locked volume "OFF". Verify on a real system.  
Confidence: suspected (read it)  

### 127. The Traffic Lab company-network guard is a heuristic that fails open
Severity: wrong result  
Where: src\Checks\Lab.cs line 28 (Company: VPN detected or DNS suffix or UserDomainName != MachineName)  
How to reproduce: an Entra/Intune-joined PC with a local account on office Wi-Fi and a VPN AdeeboMeter doesn't recognise.  
What happens / what should happen: no "my own network" tick is asked for. Say "we couldn't tell" rather than nothing. (Not bypassable from input.)  
Confidence: suspected (read it)  

### 128. Windows Update age parsed with the wrong culture
Severity: wrong result  
Where: src\Checks\SecChecks.cs line 131 (InvariantCulture tried first on InstalledOn)  
How to reproduce: en-GB system with an update installed 3/11/2026.  
What happens / what should happen: read as 11 March, wrong "days ago". Parse with the system culture or WMI date format.  
Confidence: suspected (read it)  

### 129. SNMP "n/a, never 0" is broken: empty, NULL or oversize values show as 0
Severity: wrong result  
Where: src\Checks\Snmp.cs lines 158 (ReadPdu error-status), 374 (Text), 395 (Num); src\Checks\SnmpGear.cs lines 88, 140, 270; src\Checks\SnmpTools.cs lines 242-243  
How to reproduce: an agent returning an empty INTEGER, a 9-byte INTEGER, NULL for upsEstimatedMinutesRemaining, or entPhySensorPrecision -2.  
What happens / what should happen: Text "0"; red "Runtime left 0 min" and "0% charged"; FDB port 0 "the switch itself"; PVID 0; "0.00 °C"; precision -2 read as 254; an error reply with an empty error-status accepted as data; 9-byte Counter64 truncated; 5-byte TimeTicks accepted. Return n/a and check ASN.1 types and sizes.  
Confidence: confirmed (ran it: 330,000-packet SNMP fuzz under mono)  

### 130. Overlap view flags a full-tunnel VPN (0/1 + 128/1) as overlapping the LAN
Severity: wrong result  
Where: src\Checks\PathTools.cs lines 187-193 (prefix lengths not compared)  
How to reproduce: a full-tunnel VPN that installs 0.0.0.0/1 and 128.0.0.0/1; or two addresses in one subnet on one NIC.  
What happens / what should happen: "VPN route over your LAN - printers may become unreachable" though the connected /24 still wins; red "X and X use the same addresses". Flag only when the VPN route is at least as specific and the interfaces differ.  
Confidence: suspected (read it)  

### 131. RouteFor shows "Windows has no route to it" in green
Severity: wrong result  
Where: src\Checks\RoutingTools.cs line 123  
How to reproduce: route lookup for an address with no route.  
What happens / what should happen: level 0; should be 2.  
Confidence: suspected (read it)  

### 132. Time source shows a w32tm error as a green "Current source"
Severity: wrong result  
Where: src\Checks\SysTools.cs line 298  
How to reproduce: stop the W32Time service, run Time source.  
What happens / what should happen: "The following error occurred: The service has not been started. (0x80070426)" shown at level 0; the access-denied branch computes "" in both arms. Detect the error text.  
Confidence: suspected (read it)  

### 133. TeamsUdp reports the NAT keeps "the same outside port for every server" after testing one server
Severity: wrong result  
Where: src\Checks\WhyFails.cs lines 144 and 167-170  
How to reproduce: DNS returns only one relay.  
What happens / what should happen: the same relay is queried twice so `same` is always true. Require two distinct servers.  
Confidence: suspected (read it)  

### 134. SNMP Interfaces delta: reboot and ifIndex renumbering are only partly detected
Severity: wrong result  
Where: src\Checks\SnmpTools.cs lines 99, 130, 136  
How to reproduce: sysUpTime unreadable (upt = 0), or a modular chassis that renumbers ifIndex after a reboot.  
What happens / what should happen: counters show "+N" across a restart, or deltas are computed between different ports, contradicting "a renamed port is still the same port". Compare ifDescr/ifName per index.  
Confidence: suspected (read it)  

### 135. Localized command output degrades several tools
Severity: wrong result  
Where: src\Checks\WhyFails.cs lines 55-57 (winhttp proxy labels); src\Checks\SysTools.cs lines 165-174 (gpresult regexes)  
How to reproduce: run the proxy and group-policy tools on non-English Windows.  
What happens / what should happen: proxy row reads "unknown"; gpresult falls back to a raw 200-character dump. No wrong verdict, but use language-neutral sources.  
Confidence: suspected (read it)  

### 136. Inspection decides "public CA" by name substring
Severity: wrong result  
Where: src\Checks\Inspection.cs lines 60-64  
How to reproduce: a corporate or malicious root named "DigiCert Proxy CA" in Root; or a legit public root not in the name list (TWCA, Certigna, SECOM).  
What happens / what should happen: the first counts as not inspected, the second as inspected. Compare against the AuthRoot CTL by thumbprint.  
Confidence: suspected (read it)  

### 137. SNMP community and v3 parsing oddities
Severity: wrong result  
Where: src\Checks\Snmp.cs lines 33-35 and 114 (Exchange)  
How to reproduce: a community with leading/trailing spaces or non-ASCII; a community starting with "v3:"; a closed UDP port; a forged datagram with a wrong request-id arriving first.  
What happens / what should happen: community trimmed / '?' so "wrong community"; "v3:" taken as v3; port-unreachable shown as "no answer in 3 s"; the forged packet aborts the request instead of being skipped.  
Confidence: suspected (read it)  

### 138. SNMP CPU/memory on Linux counts cache as used memory
Severity: wrong result  
Where: src\Checks\SnmpTools.cs line 206  
How to reproduce: CPU/memory view on a Net-SNMP Linux host.  
What happens / what should happen: red "Memory 98%". Subtract "Memory buffers" and "Cached memory" when present.  
Confidence: suspected (read it)  

### 139. Listening view ignores IPv6-only listeners
Severity: wrong result  
Where: src\Checks\SysTools.cs line 240 (only Proto "TCP"; IPv6 is "TCP6")  
How to reproduce: a service bound only to [::].  
What happens / what should happen: it doesn't appear among "ports open to the network". Include TCP6/UDP6.  
Confidence: suspected (read it)  

### 140. Startup list: one Run value with an illegal path character hides it and every later entry
Severity: wrong result  
Where: src\WindowsDeep\Inside.cs line 146 (ExeOf: Path.GetFileName) and lines 170-175 (one try/catch around the whole foreach)  
How to reproduce: a HKCU Run value `C:\Users\x\AppData\evil.exe"` (stray quote) or one with a tab.  
What happens / what should happen: ArgumentException drops that value and all later values from "what starts with Windows", a page meant to show persistence. Catch per value and show the raw text.  
Confidence: confirmed (ran it: mono fuzz, 1017 ArgumentExceptions in 5000 inputs; .NET flags more characters)  

### 141. M365.Role labels hosts by substring, so arbitrary domains get Microsoft roles
Severity: wrong result  
Where: src\Apps\M365.cs lines 31-32 (host.Contains)  
How to reproduce: hosts `msauth.evil.example`, `notskype.com.attacker.net`, `myoffice.com.example.net`.  
What happens / what should happen: labelled "Sign-in", "Signalling", "Office" (a phishing or tracker domain presented as Microsoft). Match on label boundaries.  
Confidence: confirmed (ran it: mono harness)  

### 142. 6 GHz channels are reported as 2.4 or 5 GHz; FromFreq gives negative channels
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 346-350 (BandOf) and line 191 (WlanApi.FromFreq); Overlaps() for 5/6 GHz  
How to reproduce: a 6E AP on an older Windows 11 build without the "Band" line; frequencies 5935 and 5925 MHz; an 80 MHz neighbour on channel 36.  
What happens / what should happen: channel 5 -> "2.4 GHz", 37 -> "5 GHz"; 5935 MHz -> ch -3; the advisor then recommends 2.4/5 GHz channels for a 6 GHz BSS; bonding ignored in overlap. Prefer WlanApi frequency, special-case 5935, use width.  
Confidence: confirmed (ran it: mono harness outputs above)  

### 143. DHCPCAPI_PARAMS stride hard-coded to 32 bytes is wrong on 32-bit Windows; no cleanup call
Severity: wrong result  
Where: src\Checks\Lab.cs line 248 (const int sz = 32; DhcpOptions), build is /platform:anycpu  
How to reproduce: DHCP options (or Security checkup's second-DHCP check) on 32-bit Windows 10.  
What happens / what should happen: the x86 struct is 20 bytes, so elements past the first are misplaced and Windows reads garbage option ids and pointers from uninitialized memory (worst case writes through one). DhcpCApiCleanup is never called. Use 20 on x86, zero the buffer, call cleanup.  
Confidence: suspected (read it)  

### 144. Process tree: PID reuse creates cycles and processes vanish
Severity: wrong result  
Where: src\WindowsDeep\Inside.cs lines 24 and 39-44 (parent by PID only, "parent must be older" not implemented)  
How to reproduce: A (pid 100, parent 200 dead) and a new B (pid 200, parent 100).  
What happens / what should happen: neither is a root so both disappear from "who started whom", or a process is attributed to an unrelated parent. Link only if the parent is older; treat unreached nodes as roots.  
Confidence: suspected (read it)  

### 145. "on battery 255%" on machines that report an unknown power status
Severity: wrong result  
Where: src\WindowsDeep\PcHealth.cs line 47 (Pressure)  
How to reproduce: a desktop or VM with ACLineStatus = 255 and BatteryLifePercent = 255.  
What happens / what should happen: "on battery 255%". Check NoSystemBattery/Unknown as Battery() does.  
Confidence: suspected (read it)  

### 146. Two sniffs in the same second share a work folder and delete each other's files
Severity: wrong result  
Where: src\Capture\LiveCapture.cs line 360 (sniff_HHmmss; CreateDirectory outside the try)  
How to reproduce: sniff two flows of the same app within one second (or Travel journey-HHmmss / final adebometer_<app>_HHmmss names).  
What happens / what should happen: files overwrite each other; mergecap fails "Merging failed" if Wireshark holds the first; a CreateDirectory error faults the task with no status. Add a random suffix; move CreateDirectory inside the try.  
Confidence: suspected (read it)  

### 147. tshark stderr containing "valid" disables RTP/DHCP extras for the rest of the session
Severity: wrong result  
Where: src\Capture\LiveCapture.cs lines 107-108  
How to reproduce: any tshark stderr like "Invalid argument" or "is not a valid interface".  
What happens / what should happen: static extrasOk = false; call quality (MOS) and DHCP server detection stay off until restart. Match tshark's real field-error message.  
Confidence: suspected (read it)  

### 148. Smaller capture and Wi-Fi slips
Severity: wrong result  
Where: src\Capture\Packets.cs line 38 (dead null check); src\Ui\FullView.cs line 886 (filters with fe80 scope); src\WindowsDeep\PcHealth.cs line 216 (Battery temp file); src\Link\LinkInfo.cs (Fields, netsh encoding)  
How to reproduce: Sniff a flow to `fe80::1%12`; run Battery from the toolbox and Investigate at once; two Wi-Fi adapters; an SSID with é or emoji.  
What happens / what should happen: invalid BPF/display filter, and Travel's SafeFilter rejects % ("Bad filter."); the fixed %TEMP%\adeebometer-battery.xml is deleted by the other run; two adapters' values are mixed; netsh output decoded as ANSI instead of OEM mangles non-ASCII SSIDs; 5 GHz advice can suggest 149-165 (restricted in some regions, 165 is 20 MHz only).  
Confidence: suspected (read it)  

### 149. A non-breaking space before % may defeat the signal parse on some locales
Severity: wrong result  
Where: src\Link\LinkInfo.cs lines 368 and 416 (TrimEnd('%', ' '))  
How to reproduce: French Windows printing `85 %` with U+00A0 (or OEM 0xFF).  
What happens / what should happen: sig = -1, weak-signal logic skipped. Trim all whitespace.  
Confidence: suspected (read it)  

### 150. tshark's own dumpcap child may escape the Job, and Stop2 kills only tshark
Severity: wrong result  
Where: src\Capture\LiveCapture.cs line 101 (Process.Start then Job.Add) and line 127 (Stop2: Process.Kill)  
How to reproduce: start and stop Live capture on an idle link; crash right after start.  
What happens / what should happen: a dumpcap spawned before Job.Add survives a crash; after Stop2 the child lives until its next write fails and may hold the adapter. Start suspended/add to job first; kill the tree.  
Confidence: suspected (read it)  

### 151. Upgrading silently re-enables Start with Windows
Severity: wrong result  
Where: src\Setup.cs line 149 (autoStart = true default) and line 227 (SetLine autostart 1)  
How to reproduce: turn Start with Windows off, then run a newer exe and click Update.  
What happens / what should happen: the switch shows on and overwrites the choice, though the card says "Your settings come along". Initialise from the existing settings.  
Confidence: suspected (read it)  

### 152. Ages, dedupe and the new outage lengths use local wall time
Severity: wrong result  
Where: src\Model.cs lines 114 and 126 (Ago, dedupe); src\Brain\Outages.cs line 30 (Pair on local T)  
How to reproduce: autumn DST change or a manual time/timezone change.  
What happens / what should happen: entries read "-3540 s", "Internet back after -59 min", identical events are suppressed for up to an hour, and an outage spanning fall-back gets a negative length in the outage log. Store UTC or monotonic times; clamp at 0.  
Confidence: suspected (read it)  

### 153. Crash-report URL can exceed GitHub's URL limit
Severity: wrong result  
Where: src\Brain\Watch.cs lines 274-277  
How to reproduce: a crash with a long stack trace, click "report it".  
What happens / what should happen: up to 3500 characters percent-encoded give 8-10k-character URLs; GitHub answers 414/400. Cap the encoded body at about 6000.  
Confidence: suspected (read it)  

### 154. Adapter choice ignores the route metric and IPv6-only networks
Severity: wrong result  
Where: src\Monitor.cs lines 282-300 (any Ethernet with an IPv4 gateway wins; Tunnel adapters skipped before IsVpn)  
How to reproduce: a USB-Ethernet link to a lab switch or a dock NIC that hands out a gateway while the internet goes over Wi-Fi; an IPv6-only / NAT64 network.  
What happens / what should happen: the wrong gateway is pinged, wrong counters and NetKey used; on IPv6-only, What's wrong? says "Windows has no default gateway ... DHCP didn't finish". Pick the best-route interface (GetBestInterface/GetBestRoute2) and accept IPv6 gateways.  
Confidence: suspected (read it)  

### 155. Settings writes use File.Replace, which fails on many SMB/NAS shares and removable drives
Severity: wrong result  
Where: src\AppCtx.cs line 216; src\Brain\Brain.cs line 110  
How to reproduce: a portable copy on a NAS or USB stick; change a setting.  
What happens / what should happen: the error is only logged, a .tmp is left, settings never update. Fall back to delete + move.  
Confidence: suspected (read it)  

### 156. Usage and first-seen app logic: newest rows dropped, update pop-ups, wrong "router still answers"
Severity: wrong result  
Where: src\Brain\Brain.cs line 100 (Store.Read(...).Take(20000)); src\Brain\Watch.cs line 39 ("router still answers") and line 133 (Conns keyed by full exe path)  
How to reproduce: usage.txt past 20,000 rows; update new Teams/Discord (versioned install paths); an internet outage while the gateway is also down; a failed ARP read.  
What happens / what should happen: the newest (current month) rows are dropped on reload; "connected to the internet for the first time" pops up after every app update (anything under \Temp\ is level 2); the inet alert says "Your router still answers" without checking; the network key flips between gateway MAC and IP, splitting one network's usage and baseline. Keep the tail, key apps by name/publisher, check the gateway.  
Confidence: suspected (read it)  

### 157. The unsigned exe's install pattern may trip Defender/SmartScreen heuristics
Severity: wrong result  
Where: src\Setup.cs (copies itself to %LOCALAPPDATA%\Programs, writes Run, copies to %TEMP% with --remove-folder, kills by name)  
How to reproduce: run the downloaded exe on a clean VM with cloud protection on.  
What happens / what should happen: the pattern matches common ML false positives (Wacatac/Sabsik); File.Copy keeps Zone.Identifier, so the installed copy still carries Mark-of-the-Web. The Store copy avoids this. Test and strip the ADS.  
Confidence: suspected (read it)  

### 158. Paths longer than MAX_PATH make settings and log fail silently
Severity: wrong result  
Where: src\Program.cs (no longPathAware manifest; File.* next to the exe)  
How to reproduce: run the exe from a deep OneDrive or corporate folder path over 260 characters.  
What happens / what should happen: PathTooLongException; settings and log fail silently (same symptom as the read-only folder). Add longPathAware or fall back to DataDir.  
Confidence: suspected (read it)  

### 159. SystemEvents handlers may run off the UI thread
Severity: wrong result  
Where: src\AppCtx.cs lines 141 and 144 (subscribed in the ctor before Application.Run)  
How to reproduce: switch the Windows theme with the engineer view open.  
What happens / what should happen: SystemEvents may raise UserPreferenceChanged on its own thread, so Theme.Apply and full.ApplyTheme() touch controls off the UI thread. Subscribe after the message loop starts or marshal with BeginInvoke.  
Confidence: suspected (read it)  

### 160. SHA256SUMS inside the release zip uses CRLF
Severity: wrong result  
Where: release.ps1 line 54  
How to reproduce: unzip a release on Linux/WSL/Git Bash and run `sha256sum -c SHA256SUMS`.  
What happens / what should happen: it looks for `AdeeboMeter.exe\r` and fails; the outer dist\SHA256SUMS uses LF. Use LF in both.  
Confidence: suspected (read it)  

### 161. Save and clipboard failures are silent
Severity: wrong result  
Where: src\Ui\FullView.Tools.cs line 47 (SaveText); src\Ui\FullView.Paint.cs and FullView.Tools.cs line 60 (Clipboard); src\Ui\Guide.cs line 148 (new "Copy for someone")  
How to reproduce: save into a read-only folder or over a file open in another app; copy while RDP holds the clipboard.  
What happens / what should happen: only logged; "Copied ✓" just never appears. Show "Couldn't save: ..." where the confirmation would appear.  
Confidence: suspected (read it)  

### 162. Changes chart isn't redrawn after reloading the same day
Severity: wrong result  
Where: src\Ui\FullView.Paint.cs lines 291-295 (histKey = yyyyMMdd|w|dark)  
How to reproduce: open History > Changes for Today, wait, click "Today" again.  
What happens / what should happen: histData reloads but the cached bitmap from the first render is shown. Include a load stamp in the key.  
Confidence: suspected (read it)  

### 163. The widget re-claims TOPMOST over the tray menu and toasts
Severity: wrong result  
Where: src\Ui\Controls.cs line 57 (HWND_TOPMOST on every Present)  
How to reproduce: Level > 0 (card open, redrawn every second), right-click the tray icon; or an auto-hide taskbar.  
What happens / what should happen: the card can cover menu items such as "Exit AdeeboMeter"; with auto-hide it sits over the revealed tray. Skip the re-assert while the menu is open; offset for auto-hide.  
Confidence: suspected (read it)  

### 164. Quitting from the tray under a modal dialog disposes the view under it
Severity: wrong result  
Where: src\AppCtx.cs line 273 (Quit: full.Close(); full.Dispose())  
How to reproduce: open Ctrl+K, Save or Export, then Exit from the tray.  
What happens / what should happen: when ShowDialog returns, code runs on a disposed form (pal.Chosen() -> ShowTab), ObjectDisposedException is logged. Close owned forms first or check IsDisposed after ShowDialog.  
Confidence: suspected (read it)  

### 165. Text boxes have no accessible name, and PillButton names and states go stale
Severity: wrong result  
Where: src\Ui\Controls.cs lines 181-190 (RoundBox), lines 109, 115, 126 (PillButton AccessibleName set once in the ctor)  
How to reproduce: Narrator on destBox, Connections filter, Find a process, guide box; then press "Start live capture" or "Run health check".  
What happens / what should happen: boxes read as "edit" (only Toolbox inputs and the new Settings search set a name); buttons keep announcing "Start live capture" after it became "Stop", "Pin" after "Unpin"; Selected and badges ("3 new", "REC") aren't exposed; disabled Primary buttons are about 1.9:1 contrast. Set AccessibleName from the cue and on text change; expose state.  
Confidence: suspected (read it)  

### 166. Tab order is reversed: page content first, navigation and header last
Severity: wrong result  
Where: src\Ui\FullView.cs lines 545-546 (controls added fill-first for docking)  
How to reproduce: press Tab from the start of the engineer view.  
What happens / what should happen: Tab visits page content before section and sub tabs, and Back/Settings/About/theme come last. Set TabIndex explicitly.  
Confidence: suspected (read it)  

### 167. High contrast is ignored and status is shown by colour alone
Severity: wrong result  
Where: src\Ui\Theme.cs line 48 (Apply: hard-coded colours only)  
How to reproduce: turn on a Windows High Contrast theme; or view with deuteranopia.  
What happens / what should happen: the app keeps its own palette, tray label and red Exit can become unreadable, and path dots, chip dot and list dots differ only by red/amber/green. Map to SystemColors in high contrast and add a glyph per level.  
Confidence: suspected (read it)  

### 168. RSA PFX backup hint is wrong: "Toolbox > Check a PFX" doesn't put the key in My keys
Severity: wrong result  
Where: src\Checks\CertKeys.cs line 323 (hint) vs src\Checks\CertTools.cs line 167 (EphemeralKeySet, never persisted)  
How to reproduce: follow the backup hint on another PC.  
What happens / what should happen: Check a PFX never imports the key. Also the passphrase must be typed exactly with dashes and upper case in the Windows wizard (unlike .adeebo), and the wizard makes the key non-exportable unless ticked, so a later backup fails. Fix the hint and explain the wizard steps.  
Confidence: suspected (read it)  

### 169. Store startup switch state may read "off" while the startup task is enabled
Severity: wrong result  
Where: src\AppCtx.cs lines 393-405 (StoreStartup.On reads an undocumented SystemAppData registry value); store\Package.appxmanifest (StartupTask Enabled="true")  
How to reproduce: fresh Store install, open Settings before ever touching the startup switch.  
What happens / what should happen: the value may be missing, so Settings shows "off" while the task runs at login. Use StartupTask.GetAsync().State, or treat missing as the manifest default.  
Confidence: suspected (read it)  

### 170. --report can say "names ... all work" while measured_ms.dns is null
Severity: wrong result  
Where: src\Report.cs line 28 (sleep for the window, default 15 s) and line 68 (dns = m.Dns.Avg(6)); src\Monitor.cs line 243 (ProbeDns runs on the 30 s internet-check pace)  
How to reproduce: `AdeeboMeter.exe --report out.json` with the default short window.  
What happens / what should happen: the monitor's DNS probe may not run inside the window, so `"dns": null` sits next to a finding that says names work (and an observed DNS row with a value from What's wrong?'s own query). Run one DNS probe before writing, or fill dns from the observed step.  
Confidence: confirmed (ran it: test build under mono, dns null while observed showed 14 ms)  

### 171. Smaller 1.1.5 UI slips: language switch, Time machine example day
Severity: wrong result  
Where: src\Ui\FullView.Settings.cs line 101 (Language changes L at once); src\Ui\FullView.Examples.cs line 87 (example events dated DateTime.Today)  
How to reproduce: switch Language to English after French without restarting; open the Time machine example from an empty past day.  
What happens / what should happen: controls built earlier keep French text (PillButton captions, cached ptItems) and only the French description says to restart; the example day's events are dated today, not the day being viewed.  
Confidence: suspected (read it)  

### 172. Pcap reader: a 63 MB capture takes about 175 MB of managed memory
Severity: slow  
Where: src\Checks\Pcap.cs line 35 (File.ReadAllBytes), lines 56-63 and 91-109 (one Pkt per frame: two IPAddress, LINQ MAC string, payload copy)  
How to reproduce: a classic pcap of 300-byte DHCP-like UDP frames filling 63 MB (184,526 packets, under both caps); open it in any replay.  
What happens / what should happen: Parse takes 320 ms but the heap grows about 111 MB on top of the 63 MB array (all LOH), far over the 64 MB budget, while each replay uses a few hundred frames. Stream the file, keep only frames the chosen replay needs, lower MaxPackets.  
Confidence: confirmed (ran it: test build under mono)  
Rule: 7  

### 173. In French, the Live capture button text is rewritten and its canvas repainted twice a second forever
Severity: slow  
Where: src\Ui\FullView.cs line 501 (OnTimer2 compares liveBtn.Text with the English "Start live capture"); PillButton Text setter translates (src\Ui\Controls.cs)  
How to reproduce: Settings > Language > Français, restart, leave the engineer view open on any tab.  
What happens / what should happen: Text is "Démarrer la capture", so the comparison is always true: every 500 ms tick sets Text (re-measure, possible layout) and invalidates liveCanvas. Keep a bool for the state or compare with L.T(...); check other `.Text ==` on PillButtons.  
Confidence: confirmed (ran it: test build under mono + Xvfb, setter ran on 5 of 5 ticks)  
Rule: 7  

### 174. Paint handlers re-spawn background work without end when a loader fails
Severity: slow  
Where: src\Ui\FullView.Travel.cs lines 52-59 and 186 (LoadApps from PaintApps), line 133 (PaintRoutes starts Routes.Read every paint); src\Ui\FullView.Insights.cs lines 18-21 (History.Week on every paint)  
How to reproduce: WMI broken or access denied while the Teams box is open; or a corrupt history row (see #7) with Insights open.  
What happens / what should happen: process enumeration or 7 day files are re-read back to back on every paint, a busy loop. Record a failure time and back off; store an empty list rather than null.  
Confidence: suspected (read it)  
Rule: 7  

### 175. HTTP request builder can hang for days and ignores Stop
Severity: slow  
Where: src\Checks\Reach.cs line 32 (ReadWriteTimeout 15 s per read) and line 65 (read loop up to 64 KB)  
How to reproduce: point the HTTP builder at a slow-loris server sending 1 byte every 14 s.  
What happens / what should happen: up to 65,536 reads (about 10 days); Reach.Http takes no Run so Stop does nothing and the Toolbox stays busy, locking every other tool until restart. Add an overall deadline and a Run parameter.  
Confidence: suspected (read it)  

### 176. Sniff captures have no size cap
Severity: slow  
Where: src\Ui\FullView.cs line 886 (dumpcap -a duration only); src\Capture\LiveCapture.cs line 366 (forward capture -a duration:15, then up to 5 ring copies and a merge)  
How to reproduce: Sniff a large download, OneDrive sync or SMB copy on a 1 Gb/s link with SniffSec 60.  
What happens / what should happen: about 7.5 GB written to %LOCALAPPDATA% (1.9 GB + ring copies + merged file with history); a small SSD fills and dumpcap's disk-full shows only as "Capture failed". Add -a filesize, a snaplen for sniffs, and a free-space check.  
Confidence: suspected (read it)  

### 177. WMI disk-health query has no timeout
Severity: slow  
Where: src\WindowsDeep\PcHealth.cs line 255 (Disk: ManagementObjectSearcher on MSFT_PhysicalDisk without Options.Timeout)  
How to reproduce: a sleeping USB disk, an iSCSI target, a Storage Spaces rebuild or a corrupt WMI repository; run Disk.  
What happens / what should happen: the thread never returns, the card stays "running", each press adds another stuck thread. Set a 10 s timeout (SecChecks.cs line 46 already does) and Wait(15000).  
Confidence: suspected (read it)  

### 178. Stream overlay rewrites overlay.png every second and never pauses
Severity: slow  
Where: src\Ui\StreamOverlay.cs lines 21, 46 and 101-113  
How to reproduce: open the stream overlay with PNG on, outside a game.  
What happens / what should happen: a PNG encode and File.Replace every second even when nothing changed (86,400 writes a day; up to 30 identical frames per 30 s sample); "the last 60 seconds" graph shows about 30 minutes of data outside a game; the PNG is opaque, not transparent; the window has no owner and outlives the view; strings aren't translated. Write only on change, label the real window, clear transparent.  
Confidence: suspected (read it)  
Rule: 7  

### 179. CMS (.p7m) encrypt and decrypt hold up to 200 MB several times over in memory
Severity: slow  
Where: src\Checks\FileCrypt.cs line 235 (ReadAllBytes -> ContentInfo -> Encode -> WriteAllBytes) and line 279 (Decode(ReadAllBytes))  
How to reproduce: encrypt a 200 MB file to an RSA recipient (.p7m), on a 4 GB PC or ARM tablet.  
What happens / what should happen: about 600-800 MB peak and LOH fragmentation; OutOfMemoryException is not caught ("Couldn't finish"). Lower the limit (about 50 MB) or point to the streaming .adeebo modes.  
Confidence: suspected (read it)  
Rule: 7  

### 180. Log has no rate limit, wipes itself at 256 KB, uses culture time and raw newlines
Severity: slow  
Where: src\Program.cs lines 148-158 (Log); src\AppCtx.cs line 344 (OnUi logs every 50 ms on a persistent error)  
How to reproduce: any persistent OnUi exception (e.g. PrimaryScreen null in a disconnected RDP session), or th-TH culture.  
What happens / what should happen: 20 open/append/close per second (AV scanning in Downloads); File.Delete at 256 KB throws away the earliest, most useful lines every few minutes; Buddhist year or "." time separator; network strings with CR/LF go in raw. Rate-limit repeats, roll to .log.1, InvariantCulture, escape CR/LF.  
Confidence: suspected (read it)  
Rule: 7  

### 181. Brain.Bg starts a new OS thread every 5 seconds
Severity: slow  
Where: src\Brain\Brain.cs lines 76-81 (Bg)  
How to reproduce: leave AdeeboMeter running for a day.  
What happens / what should happen: about 17,000 thread creations a day for small periodic work. Use one long-lived low-priority worker.  
Confidence: suspected (read it)  
Rule: 7  

### 182. AutoScroll can oscillate on long painted pages
Severity: slow  
Where: src\Ui\FullView.Inside.cs line 230 (Need sets AutoScrollMinSize from inside Paint) and the same pattern in every Paint* method  
How to reproduce: size the window so a page's content is within about one wrapped line of the client height (Settings, About, Guide, Learn, Link).  
What happens / what should happen: the scrollbar appears, width narrows, more lines wrap, then the bar is not needed and disappears, repeatedly: flicker and CPU. Always reserve the scrollbar width when laying out.  
Confidence: suspected (read it)  
Rule: 7  

### 183. Settings and Inside pages do disk and process I/O inside Paint
Severity: slow  
Where: src\Ui\FullView.Settings.cs lines 132 and 232 (History.Days(), History.Bytes()); src\Ui\FullView.Inside.cs line 203 (Native.ExePath per repaint)  
How to reproduce: open Settings or Inside on a slow or AV-scanned disk, scroll.  
What happens / what should happen: directory enumeration and process queries on every repaint ("boot" mode reads StartTime for 15 entries; Procs/Changes paint every row with two Explain.Find calls). Cache per refresh tick.  
Confidence: suspected (read it)  
Rule: 7  

### 184. Tray tooltip is rewritten on every render once it is over 63 characters
Severity: slow  
Where: src\AppCtx.cs line 361 (compares the untruncated tip with tray.Text)  
How to reproduce: a tooltip longer than 63 characters while the card is open.  
What happens / what should happen: NIM_MODIFY every second. Compare against the truncated string.  
Confidence: suspected (read it)  
Rule: 7  

### 185. PhysicalDisk performance counters can block the sampler at start
Severity: slow  
Where: src\Monitor.cs lines 122-124 (English counter names "PhysicalDisk", "% Idle Time")  
How to reproduce: localized Windows or a corrupted perf-counter registry; start the app.  
What happens / what should happen: the failure is handled (Disk shows NaN) but the first construction can block the sampler thread for seconds. Create them off the sampler thread with a timeout.  
Confidence: suspected (read it)  

### 186. GPU tool creates up to 400 PerformanceCounter instances per press
Severity: slow  
Where: src\WindowsDeep\PcHealth.cs lines 67-71 (GPU Engine instances, Take(400))  
How to reproduce: run the GPU tool on a PC with many GPU processes.  
What happens / what should happen: several seconds of PDH/registry calls per press. Aggregate per process or use one category read.  
Confidence: suspected (read it)  

### 187. SNMP Snapshot limit message is wrong and Stop is slow on v3
Severity: slow  
Where: src\Checks\SnmpGear.cs line 21 (180 s deadline); src\Checks\Snmp.cs line 351 ("stopped at the 2-minute limit")  
How to reproduce: run Snapshot against a slow v3 agent and press Stop.  
What happens / what should happen: the message says 2 minutes for a 3-minute limit; Stop is checked only between requests, and one v3 request can block about 12 s, with the 1 MB password stretch recomputed for every message. Fix the text, cache localized keys per engine.  
Confidence: suspected (read it)  

### 188. Screen.PrimaryScreen can be null and floods the log 4 times a second
Severity: slow  
Where: src\AppCtx.cs line 289 (WidgetScreen().WorkingArea in OnUi)  
How to reproduce: disconnected RDP session, or all displays powered down on some GPUs.  
What happens / what should happen: NullReferenceException every 250 ms, caught and logged until the log wipes at 256 KB. Null-guard and rate-limit.  
Confidence: suspected (read it)  

### 189. "On SR OS" in the TWAMP hint is a Nokia reference
Severity: cosmetic  
Where: src\Checks\Protocols.cs line 418 (Twamp: "On SR OS: a TWAMP-light reflector on the router")  
How to reproduce: Toolbox > TWAMP-light with an empty or unparsable reflector box; read the hint.  
What happens / what should happen: SR OS is Nokia's router OS, which rule 8 forbids anywhere in the product. Reword to "On a router: a TWAMP-light reflector; on Linux: any TWAMP-light reflector." (grep found no other Nokia / SR OS / TiMOS / 7750 hits in src, knowledge, docs or README.)  
Confidence: confirmed (ran it: grep of the dev tree and the string in the test build)  
Rule: 8  

### 190. French is a mixed-language preview: the new Toolbox home, all tool titles and many new screens are English
Severity: cosmetic  
Where: src\Ui\FullView.ToolHome.cs lines 71-145; Tools2 titles via L.T(t.Title) in src\Ui\FullView.Toolbox.cs; lang-fr.txt (323 entries, duplicate key "Simple" at lines 161 and 236)  
How to reproduce: Settings > Language > Français, open Toolbox, Points, Gamer Zone, Fix a problem.  
What happens / what should happen: 129 of 129 tool titles, 7 of 8 Toolbox questions and 18 literal L.T strings ("All tools", "Nothing matches - try another word", "Did you mean", the About donation line...) have no French entry; Fix-a-problem hints, Points rows, Gamer statuses, StreamOverlay, GameEditor, outage log and Settings descriptions never go through L.T. Sentences are glued from fragments (`L.T("to")`, `L.T(" tools match")`), so word order can't be translated. Use whole-sentence keys with {0}; ship a language only when complete (the plan's own rule).  
Confidence: confirmed (ran it: Python parser over lang-fr.txt and the source; reflection on the test build's embedded table)  

### 191. French text is clipped in fixed-size rectangles on the new screens
Severity: cosmetic  
Where: src\Ui\FullView.Overview.cs lines 277, 291, 300-301 (Fix intro, hint, symptom tiles); src\Gamer\Gamer.cs lines 118 and 283-287 (tile, card header); src\Ui\FullView.ToolHome.cs line 114 (Lab note)  
How to reproduce: Français at 100% scale; Overview symptom tiles, Fix a problem, Gamer card header.  
What happens / what should happen: "Le ventilateur souffle ou ça chauffe" wraps to a hidden second line; "Arrêter la surveillance" (about 150 px) is in a 120*S box and "Modifier les serveurs" in 100*S; the Fix hint's "Start >" is cut even in English on narrow windows. Measure and wrap or ellipsize.  
Confidence: suspected (read it; widths estimated from font sizes)  

### 192. Labels are measured in English but drawn translated, so right-aligned text is mispositioned
Severity: cosmetic  
Where: src\Ui\FullView.Paint.cs line 87 ("Windows counters · no admin"); src\Ui\Theme.cs lines 290-291, 355-356, 404-405 (GamerCard/MetricCard nw for "AdeeboMeter")  
How to reproduce: Français, look at the Windows counters label and the widget card name.  
What happens / what should happen: the width of the English string is used to place the translated one, so it overlaps or leaves a gap. Measure L.T(...) as drawn.  
Confidence: suspected (read it)  

### 193. Capture status always says "30 s", and the Live capture label goes stale
Severity: cosmetic  
Where: src\Ui\FullView.cs line 891 ("Listening in for 30 s on " hard-coded); live capture label built once at view creation  
How to reproduce: Settings > Sniff = 60 s, Sniff a flow; change the live-capture limit with the view open.  
What happens / what should happen: says 30 s while dumpcap runs 60 s; the "after N min" label keeps the old value until the view is reopened. Use Cfg.SniffSec and refresh in ShowTab.  
Confidence: suspected (read it)  

### 194. Font.GetHashCode as the measure-cache key collides across fonts
Severity: cosmetic  
Where: src\Ui\Theme.cs line 212 (Measure cache key)  
How to reproduce: measure the same string with fBody and fBodyB (or fSmall and fMono at 100%, fSmall and fMonoS at 125%).  
What happens / what should happen: .NET Framework's Font hash ignores the family and truncates the size, so the first font's size is returned and wrapped Semibold or monospace text is clipped or overlaps. Key on name, size and style, or per font instance.  
Confidence: suspected (read it)  

### 195. Detail card, tips and some painted rows aren't clamped or clipped
Severity: cosmetic  
Where: src\Ui\Theme.cs line 496 onwards (DetailCard placement); src\Ui\Controls.cs line 503 (TipWin.ShowAt clamps x/y only)  
How to reproduce: a short screen (landscape tablet at 200%), a long tip, a 32-byte SSID, a long host in the dest title, the footer with the live-capture suffix at 980*S.  
What happens / what should happen: the card's top goes off-screen or onto the next monitor, tips are cut at the bottom, unclipped DrawString runs under neighbouring content. Clamp to the working area and draw into rectangles with ellipsis.  
Confidence: suspected (read it)  

### 196. Draw.Trunc cuts surrogate pairs
Severity: cosmetic  
Where: src\Ui\Theme.cs line 226 (Trunc)  
How to reproduce: an emoji SSID, pin name or adapter name long enough to be truncated.  
What happens / what should happen: a lone high surrogate renders as a box. Step back one char when cutting inside a pair.  
Confidence: suspected (read it)  

### 197. Segoe UI Variable under GDI+, and missing CJK/emoji glyphs
Severity: cosmetic  
Where: src\Ui\Theme.cs lines 189-190 (Draw.Fam picks Segoe UI Variable on Windows 11)  
How to reproduce: Windows 11, bold text; CJK process descriptions or emoji SSIDs.  
What happens / what should happen: bold may be synthetically smeared, and GDI+ fallback shows boxes. Prefer Segoe UI for GDI+ text.  
Confidence: suspected (read it)  

### 198. ThemeSwitch and the new SlideSwitch expose no checked state
Severity: cosmetic  
Where: src\Ui\Controls.cs lines 447-455 (ThemeSwitch: default role, always draws a focus ring), line 427 (SlideSwitch: Role=CheckButton but no state)  
How to reproduce: Narrator on the theme switch or the Details switch.  
What happens / what should happen: the reader can't tell which state is on. Report Checked; honour ShowFocusCues.  
Confidence: suspected (read it)  

### 199. Connections list has no keyboard use, text scaling is ignored, widget is hover-only
Severity: cosmetic  
Where: src\Ui\Controls.cs line 211 (ConnList: TabStop but no arrow/Enter handling); fonts fixed in Theme.cs  
How to reproduce: Tab into Connections and use arrows/Enter; set Windows "Make text bigger"; try to read the widget with the keyboard.  
What happens / what should happen: no row selection or Enter-to-Sniff; text size ignored; keyboard users get only the 63-character tray tooltip. Add key handling, honour the text scale, put the headline in the tooltip.  
Confidence: suspected (read it)  

### 200. Program.cs header and the self-test overstate privacy
Severity: cosmetic  
Where: src\Program.cs lines 5-8 (header comment); src\SelfTest.cs line 31 ("nothing on the internet") and line 219 (DestCheck.Run(gw + ":80") can call Portal())  
How to reproduce: read the header; run the self-test where the router has no port 80.  
What happens / what should happen: "the only packets it sends" omits the per-network trace + reverse DNS, a pinned host's DNS every 30 s, the 1.1.5 TCP:443 fallback and the Gamer Zone; "everything it keeps lives next to the exe" ignores %LOCALAPPDATA% history/usage/baselines; the self-test can request www.msftconnecttest.com. Update the text.  
Confidence: suspected (read it)  

### 201. Uninstall leaves %TEMP%\AdeeboMeter-remove.exe behind
Severity: cosmetic  
Where: src\Setup.cs line 196 (helper copied to %TEMP%, never deleted)  
How to reproduce: uninstall, look in %TEMP%.  
What happens / what should happen: the helper stays forever. Sweep it at next start or use MoveFileEx delay-until-reboot.  
Confidence: suspected (read it)  

### 202. Small wrong messages in the encrypt and decrypt tools
Severity: cosmetic  
Where: src\Checks\FileCrypt.cs lines 191, 267, 287 (extension-based detection), name Take(255); src\Ui\FullView.Toolbox.cs line 259 (pub overwrite)  
How to reproduce: rename a code-mode .adeebo to file.bin and open it; a mode-2 file with a bad MAC; a recipient named "code"; a long UTF-8 name.  
What happens / what should happen: file.bin goes to the CMS branch ("ASN1 corrupted data" + "wasn't made for any of your keys"); a mode-2 MAC failure says "Check the code..."; a recipient called "code" can't be chosen; Take(255) can split a UTF-8 character (U+FFFD); EcAction("pub") overwrites an existing public key file. Detect by the 8-byte magic.  
Confidence: confirmed (ran it: harness h3 on the test build for the renamed file); the rest is read  

### 203. Hostile mode-2 ephemeral key types throw ArgumentException, not "damaged"
Severity: cosmetic  
Where: src\Checks\EcKeys.cs line 97 (CngKey.Import + DeriveKeyMaterial), ephPub from lines 133 and 138 unchecked  
How to reproduce: a recipient block with your key id but an ECDSA P-384 or ECDH P-256 ephemeral blob.  
What happens / what should happen: "Couldn't finish" plus a stack trace in the log (nothing written; the temp file is deleted). Check length 104 and the magic before import; map to "damaged".  
Confidence: suspected (read it; CNG not on mono)  

### 204. ICMP translator prints a bogus port for an inner header with IHL < 5
Severity: cosmetic  
Where: src\Checks\Replay.cs lines 186-189 (Inner)  
How to reproduce: an ICMP 3/4 quoting an IPv4 header starting 0x40 (IHL 0).  
What happens / what should happen: "About: TCP to 5.6.7.8:0" (port read from inside the IP header). Require IHL >= 5.  
Confidence: confirmed (ran it: test build under mono)  

### 205. Unsupported link types (802.11, radiotap) read as "no DHCP / no EAPOL in this capture"
Severity: cosmetic  
Where: src\Checks\Pcap.cs line 104 (`else return null;`)  
How to reproduce: open a Wi-Fi monitor-mode capture (link type 105 or 127) in any replay.  
What happens / what should happen: 0 packets and advice to capture again. Say "link type N isn't supported". (SPB timestamps reuse the previous packet's.)  
Confidence: confirmed (ran it: unknown link types return 0 packets on the test build)  

## Website and docs

### 206. privacy.html (the Store listing's policy) leaves out most of the traffic, and 1.1.5 widens the gap
Severity: wrong result  
Where: docs\privacy.html lines 43-49 ("What it sends, and when"); compare src\Ui\FullView.Settings.cs lines 369-385 (in-app About)  
How to reproduce: read privacy.html "What it sends", then About > "What leaves your PC" in the 1.1.5 build.  
What happens / what should happen: it lists only the 1.1.1.1/8.8.8.8 ping, tools you start and "online lookups (for example DoH)". Missing: router ping, DNS with each check, the per-network trace + reverse DNS, msftconnecttest, pinned destinations, RIPEstat and PeeringDB by name, and in 1.1.5 the speed test (speed.cloudflare.com, Vultr, proof.ovh.*), Gamer Zone pings and api.steampowered.com, the TCP:443 fallback to 1.1.1.1 and the ISP report's cloudflare.com/cdn-cgi/trace. "DoH only with online lookups" is false for the Encrypted DNS tool. Rebuild the list from About's table.  
Confidence: confirmed (ran it: compared the page text with the About table and the code; docs\ unchanged on dev-1.1.5)  

### 207. "Online lookups only send public IPs to RIPEstat and PeeringDB" is false; About's row says the same
Severity: wrong result  
Where: README.md lines 78 and 101; docs\index.html lines 824 and 965; About row src\Ui\FullView.Settings.cs line 380; code src\Checks\SecChecks.cs lines 365 and 381  
How to reproduce: turn on online lookups, run Security checkup.  
What happens / what should happen: it sends a DoH query for www.microsoft.com to cloudflare-dns.com and opens TLS to www.microsoft.com:443, names not IPs, to neither RIPEstat nor PeeringDB. Add a row "Security checkup (with online lookups): one DoH question to Cloudflare, one TLS handshake to Microsoft".  
Confidence: suspected (read it)  

### 208. Website header overflows on phones narrower than 388 px
Severity: cosmetic  
Where: docs\index.html lines 50-57 and 295-298 (.top .wrap flex row, no wrap or shrink); docs\privacy.html (long link)  
How to reproduce: Playwright iPhone SE (320) or a 360/375/384 px viewport, read innerWidth/scrollWidth.  
What happens / what should happen: layout viewport grows to 388 px, the Download pill is cut ("Do" at 320 px) and the page pans sideways; privacy.html is 339 px wide at 320. Shrink the brand/pill below 400 px; `a{overflow-wrap:anywhere}`.  
Confidence: confirmed (ran it: Playwright/Chromium, screenshots nav-375.png, nav-iPhone_SE.png)  

### 209. With api.github.com unreachable the site shows stale versions and the in-app "Check for updates" shows nothing
Severity: wrong result  
Where: docs\index.html line 21 (JSON-LD 1.1.3), 363 ("Version 1.1.3"), 1003 ("Version 1.0"), 1088-1108 (banner only inside the API .then)  
How to reproduce: load `/?v=1.1.4#update` with api.github.com answering 403 (rate limit, ad blocker, corporate proxy).  
What happens / what should happen: no banner, "Version 1.1.3" and "Version 1.0"; an app user who clicked Check for updates gets no answer. Update the fallbacks each release; when the fetch fails and ?v= is present, say "Couldn't check right now - see GitHub Releases". (Dev also still sends ?v=1.1.4, see #43.)  
Confidence: confirmed (ran it: Playwright with the API blocked, pw9.js)  

### 210. PageUp/PageDown are hijacked across the page while the slide deck is in view
Severity: wrong result  
Where: docs\index.html lines 1038-1043  
How to reproduce: desktop 1366x768, scroll the deck into the middle, press PageDown repeatedly.  
What happens / what should happen: scrollY stays at 3082 while the counter goes 2/26, 3/26...; up to 25 presses stuck; arrows are also taken globally. Handle keys only when the deck has focus.  
Confidence: confirmed (ran it: Playwright, pw7.js)  

### 211. About 880 px blank gap under most slides on phones
Severity: cosmetic  
Where: docs\index.html lines 185-188 and 302-303  
How to reproduce: iPhone 13 emulation, scroll to "Every part, in plain words."  
What happens / what should happen: the stage is as tall as the tallest slide (1273 px), so a 398 px slide has about 880 px of empty space before "1 / 26". Size to the current slide or stack slides on mobile.  
Confidence: confirmed (ran it: Playwright, phone-dark-full.png)  

### 212. Dark-mode buttons and the Donate link fail WCAG AA contrast
Severity: cosmetic  
Where: docs\index.html lines 33, 38, 55, 57, 83  
How to reproduce: axe-core on / with colorScheme dark.  
What happens / what should happen: white on #4C8DF6 is 3.25:1 (Download buttons), Donate #D1245B on #0A0E13 is 3.79:1; light-mode ghost buttons 4.04:1, .meta 4.49:1; footer "MIT License" link differs by 1.6:1 with no underline. Darken/lighten as needed and underline links.  
Confidence: confirmed (ran it: axe-core 4.10.2 in Playwright)  

### 213. "103 tools" is wrong: dev-1.1.5 registers 129
Severity: cosmetic  
Where: README.md line 32; CHANGELOG.md line 7; docs\THEORY-OF-EVERYTHING.md line 235  
How to reproduce: count unique T("id", ...) entries in Tools2() (src\Ui\FullView.Toolbox.cs).  
What happens / what should happen: 129 unique tool ids (was 105 in 1.1.4). Say the real number or "100+", and assert it in the self-test.  
Confidence: confirmed (ran it: counted the registry in source; 129 ids also matched by the examples check)  

### 214. Stale version strings across the docs
Severity: cosmetic  
Where: docs\index.html lines 21, 363, 451, 873, 1003; docs\THEORY-OF-EVERYTHING.md line 3 and the PDF; .github\ISSUE_TEMPLATE\problem.yml  
How to reproduce: grep for 1.1.3 / "Version 1.0" / "AdeeboMeter 1.1".  
What happens / what should happen: 1.1.3, 1.0 and 1.1 appear while the app is 1.1.4 (and soon 1.1.5). Bump them and add a grep step to the release checklist.  
Confidence: confirmed (ran it: grep and pdftotext)  

### 215. Spoofable update banner text via ?v=
Severity: security  
Where: docs\index.html lines 1099-1104  
How to reproduce: open `/?v=Please+download+the+fix+from+evil.example#update`.  
What happens / what should happen: on the official domain the blue banner reads "AdeeboMeter 1.1.4 is out - you have Please download the fix from evil.example..." (textContent, so no XSS); `?v=2.0` says "up to date - 2.0 is the latest". Accept only a version pattern; say "you're ahead" for newer.  
Confidence: confirmed (ran it: Playwright, update-banner-spoof.png)  

### 216. privacy.html is an orphan page
Severity: cosmetic  
Where: docs\index.html (no link to privacy.html); docs\sitemap.xml  
How to reproduce: `grep -n privacy.html docs/index.html`.  
What happens / what should happen: no link anywhere and not in the sitemap. Link it from the footer and the policy card; add to the sitemap.  
Confidence: confirmed (ran it: grep)  

### 217. Sitemap lastmod is stale and there is no robots.txt
Severity: cosmetic  
Where: docs\sitemap.xml  
How to reproduce: compare lastmod (2026-11-09) with index.html's last change (2026-11-11); look for docs\robots.txt.  
What happens / what should happen: stale lastmod, no robots.txt advertising the sitemap. Update and add one.  
Confidence: confirmed (ran it: git log and file check)  

### 218. Slide footer numbers disagree with the counter
Severity: cosmetic  
Where: docs\index.html (slide .foot spans)  
How to reproduce: page through the deck.  
What happens / what should happen: 26 frames, footers read 01, 01, 02 ... 24, 24; static fallback "1 / 24". Renumber or generate in JS.  
Confidence: confirmed (ran it: Playwright)  

### 219. "One readable file / One C# file" is no longer true
Severity: cosmetic  
Where: docs\index.html line 519; CONTRIBUTING.md line 20  
How to reproduce: count src\*.cs.  
What happens / what should happen: dozens of .cs files (more in 1.1.5). Say "Readable source - plain C#".  
Confidence: confirmed (ran it: file count)  

### 220. Self-test claims are out of step: "30 checks" (now 58) and "under 1 second" (test allows 2 s)
Severity: cosmetic  
Where: README.md line 83; docs\index.html line 869; docs\THEORY-OF-EVERYTHING.md lines 281 and 285  
How to reproduce: count T("...") checks in src\SelfTest.cs; read the timing assert.  
What happens / what should happen: SelfTest.cs registers 58 checks (was 35); the engineer-view test fails only above 2000 ms. Fix the numbers.  
Confidence: confirmed (ran it: counted in source)  

### 221. The page says it loads nothing from elsewhere but calls api.github.com on every visit
Severity: cosmetic  
Where: docs\index.html line 24 (comment) and line 1088 (fetch)  
How to reproduce: load the page and watch requests.  
What happens / what should happen: every visitor's IP and Referer go to GitHub's API (200 seen). Fix the comment and mention it in privacy.html.  
Confidence: confirmed (ran it: Playwright request log)  

### 222. The narrated tour has no captions; the trailer autoplay ignores reduced motion
Severity: cosmetic  
Where: docs\index.html line 419  
How to reproduce: play the tour; set prefers-reduced-motion.  
What happens / what should happen: no track element (WCAG 1.2.2); the trailer autoplays and loops anyway and downloads 2.6 MB on mobile. Add captions; pause under reduced-motion or saveData.  
Confidence: confirmed (ran it: Playwright, DOM check)  

### 223. Keyboard and screen-reader polish on the site
Severity: cosmetic  
Where: docs\index.html line 185 (.stage outline:none) and line 296 (nav hidden under 860 px)  
How to reproduce: Tab to the carousel; use a screen reader on the theme switch; open on a phone.  
What happens / what should happen: no visible focus on the deck, the theme switch has no aria-pressed, and mobile users can't reach Install/Feedback/GitHub. Add focus-visible, aria-pressed and a compact menu.  
Confidence: confirmed (ran it: Playwright)  

### 224. privacy.html ignores the visitor's chosen theme
Severity: cosmetic  
Where: docs\privacy.html lines 12-14  
How to reproduce: pick Light on index.html with the OS in dark mode, open privacy.html.  
What happens / what should happen: dark page; no script reads localStorage am-theme. Copy the theme line from index.html.  
Confidence: suspected (read it)  

### 225. README says "offline" for an app that pings the internet by default
Severity: cosmetic  
Where: README.md lines 9 and 13  
How to reproduce: read the badge alt text and the tagline.  
What happens / what should happen: by default it pings 1.1.1.1 and resolves a name every 30 s (and 1.1.5 adds more background traffic). Use "local-first" like the site.  
Confidence: confirmed (ran it: file content)  

### 226. The suggestion box may drop the user's text for Ideas, and the bug label may not apply
Severity: wrong result  
Where: docs\index.html lines 1077-1083; .github\DISCUSSION_TEMPLATE\ideas.yml  
How to reproduce: submit an idea from the site's suggestion box.  
What happens / what should happen: the Ideas category uses a form template, which prefills by field id (`idea`), not body=, so the text may be lost; labels=bug applies only for triagers; a 4000-emoji body builds a 24k-character URL. Use &idea=, a template for problems, and cap the URL.  
Confidence: suspected (read it; github.com discussions blocked by the proxy)  

### 227. privacy.html's Microsoft Store section describes a listing that isn't live yet
Severity: cosmetic  
Where: docs\privacy.html lines 54-55  
How to reproduce: read the section; search the Store.  
What happens / what should happen: 1.1.5 now builds the Store package, but the listing isn't live; keep the section only once it is (or say "if available").  
Confidence: suspected (read it)  

### 228. Docs say the Wi-Fi advisor and look-alike detection work without location; the code returns before both
Severity: wrong result  
Where: docs\THEORY-OF-EVERYTHING.md line 226; README.md line 29; code src\Link\WifiAdvisor.cs lines 77-81  
How to reproduce: turn location off, open Your connection > Wi-Fi.  
What happens / what should happen: only the health score, power-saving advice and walk test work; channel, band, sticky-AP and twin detection need location. Fix the docs or show "needs location" placeholders.  
Confidence: suspected (read it)  

## Checked and held up

- .adeebo crypto: the KDF really is PBKDF2-HMAC-SHA256 (test build output equals Python's hashlib byte for byte); the native BCryptDeriveKeyPBKDF2 signature is right. An independent Python decrypt of a test-build file using only the documented layout gave MAC ok and the right plaintext.
- Encrypt-then-MAC with separate keys; the MAC covers magic, version, iterations, salt, IV and all ciphertext and is checked in constant time before decrypting. All 44 header-byte flips, truncation, appending, empty and header-only files are refused; a changed version byte is refused (no downgrade); a mode-3 backup can't be opened as mode 1; the iteration field is bounded to 100k-2M.
- NewCode uses RandomNumberGenerator with rejection sampling: 20,000 codes, all 31 symbols within 15,321-15,622 (expected 15,484), no duplicates, about 119 bits. Fresh salt/IV per file, random file key and new ephemeral ECDH key per recipient in mode 2, recipient blocks under the MAC.
- No temp file is left on a wrong code or refused file; FreeName blocks path escape on both separators. Generated codes and recovery passphrases are kept out of Copy, Ctrl+C, Save, CSV and the last result.
- --report JSON escaping is valid for quotes, backslashes, control characters, U+2028 and lone surrogates.
- Policy: NoLab is now checked in Lab.Allowed; an oversized exe-dir policy falls through to ProgramData; policy mode locks the Settings row.
- Store package: only runFullTrust; --remove-folder, Uninstall and the Run key are skipped when packaged; the exe folder (WindowsApps) can't take a planted policy or portable marker.
- pcap/pcapng reader and the DHCP, RA, 802.1X and ICMP replays: 530,000 fuzzed inputs and every crafted length (block length 0/13/huge, caplen 0xFFFFFFFF, 60 MB of tiny blocks, 20 chained IPv6 headers, odd RA/DHCP/EAPOL options) gave no crash, hang or exception; max 60 ms per input.
- RADIUS: 200,000 random replies through DecodeAttrs and ReplyOk without an exception; the secret and password are never logged, shown, kept in shown/key, and arrays are wiped; replies from other sources are filtered by Connect; wrong identifier or authenticator rejected.
- ISP report: the public IP is fetched only when the user types "yes" and appears nowhere else. Find my match server builds tshark arguments from a parsed GUID and integer ports only and honours NoCapture.
- Game editor validates ports (1-65535) and hosts; games.txt uses TryParse with ranges. UdpMtu sends at most about 26 packets to the one target with DF set. Traffic Lab DSCP gap is clamped and counts are capped in the sender, not the UI.
- Examples are never saved, copied, compared or counted: ResultText, SaveResult and Ctrl+C check IsExample, Compare is disabled, MTU/MTR live tables are hidden, tbLast/tbRecent untouched, Points.Completed refuses example rows, Fill never writes password boxes and never runs a tool, and Lab tools still need the tick after Fill.
- Points: each id awards once; replay +2 once, only for the 8 replay ids, at 10 minutes or more; nothing earned with the switch off (Award, PageUsed, LearnRead); the total survives a clean restart; junk lines and NaN skipped.
- 129 tools, 130 example ids, no missing or duplicate example; every tool has a terms.txt entry. French table: no malformed lines, no placeholders, so no FormatException risk.
- New pages don't repaint idly (LearnPending once at 15 s; Gamer every second only while a game is watched). Toolbox search is safe with empty, regex-character, huge, Unicode and Turkish İ input; Fix-a-problem never crashes on empty, emoji or 10,000-character input.
- SNMP decoder: 330,000 malformed packets, no crash or hang (3 ms worst); every TLV bounded, varbinds capped at 5000, 5,000-deep nesting harmless. Replies accepted only from the target (Connect, request-id, community; v3 message-id, engine-id, user, HMAC, time window; privacy can't be stripped).
- Input tools: SafeHost/SafeUser for remote-access commands are tight; every Toolbox run is inside StartTool's catch; Stop keeps tbBusy until the worker ends; Json.Parse has a depth cap; DNS/STUN/varbind/DHCP/rootDSE parsers survived 15,000 fuzz cases each; the HTTP builder strips CR/LF from headers and caps the body; TLS checks send no data over an untrusted handshake; no Process.Start takes raw user input.
- Capture: no home-made packet parser for live traffic (tshark does the decoding); tshark field output, LLDP/CDP text, WLAN IEs, RTP stats and DisplayDns survived 20,000 fuzzed inputs each; child tools sit in a kill-on-close Job; Wireshark launch arguments and BPF filters are built from parsed addresses and ints; M365 labelling contacts nothing.
- Native struct layouts (TCP/UDP owner tables v4/v6, DNS_RECORD, WLAN_INTERFACE_INFO, WLAN_BSS_ENTRY, MIB_IPNETROW, ENUM_SERVICE_STATUS_PROCESS, job object, MEMORYSTATUSEX) checked offset by offset; only DHCPCAPI_PARAMS on x86 is wrong.
- UI: no per-paint GDI leak (brushes, pens, paths, cached bitmaps, HBITMAP/DC all released); every BeginInvoke from a task is guarded; busy flags prevent duplicate runs; Clipboard calls are wrapped; widget opacity can't go below 20%; an unplugged monitor falls back to the primary screen.
- Background loops are wrapped in try/catch; no background exception path ends the process except the ones listed above (#1, #12).
- Website: no console errors or 404s at any size or scheme; all anchors resolve; every img has alt text; videos are fast-start; the download asset name and SHA256 match the release; "42 security checks" matches the code; the suggestion box URL-encodes input (no XSS).

## Self-test gaps

- The self-test asserts a wrong behaviour: SelfTest.cs line 417 expects destination-only loss to be "real loss starts at hop N" (#44). Hop patterns from #58 and #59 are not tested.
- Verdict logic (Monitor.Judge): no cases for offline/airplane, gateway that ignores ICMP, a gateway that once replied, IPv6-only, Internet check off, Wi-Fi without netsh.
- Setup: Install, Uninstall, upgrade, downgrade, Run-key quoting, "run here" and portable autostart, old-version cleanup never run; only Under() and SetLine().
- Settings file I/O (LoadSettings/SaveSettings) with garbage, empty, huge or read-only files; settings Import.
- History: Minute/Event write, Tidy (.csv and .gz together), Load of corrupt/negative/future rows, Week(); the outage log's Pair with network changes and DST.
- Brain, Watch, Devices, Alerts: usage sums and caps, baseline after a network switch, first-seen apps, alert cooldowns and quiet hours.
- ProbeWifi's netsh parser (English, localized, location-blocked) and Ring/Timeline (wrap, NaN, negative Ago, dedupe); no clock-jump test.
- CLI parsing and the single-instance hand-off; Crash.Clean and IssueUrl length.
- "never listens" is vacuous (AppCtx is never constructed); many network items pass on "didn't throw".
- Policy: both files present (exe dir + ProgramData), `mode = pro`, speed `check` under `speed_test = off`.
- --report: verdict when a step is level 3; exit codes for usage vs I/O errors.
- Points: corrupt points.txt, Lab-gated tools earning (only udpsend is checked), a failed load overwriting the file, PageUsed earning.
- Language: French coverage for literal L.T keys and tool titles, the duplicate key "Simple", placeholder parity, `.Text == "English"` comparisons on translated PillButtons.
- FixMatch: only six happy phrases (SelfTest.cs lines 392-404); none with "work" or "not working".
- Games: empty or emoji names in games.txt, GameEditor id collisions, quoted exe paths, server-name collisions after truncation.
- Examples: THE PC tools' examples reachable; `in:` lines use documentation addresses only (a regex in ExampleCheck).
- StreamOverlay has no test; Toolbox search not tested in French.
- Hotkey: the test only checks Ctrl+Alt+A is free (SelfTest.cs line 789), not AltGr layouts.
- FreeName's self-test fails under mono only because SelfTest.cs line 638 trims '\' but not '/'; harmless on Windows, but the test is platform-fragile.
- Crypto: more than 20 recipients, malformed keys-ec.txt lines, key-backup names with CR/LF or '|'.
- Speed test: GradeOf with no loaded samples, a 302 server, Stop during upload.
- pcapng with if_tsresol other than 6.
