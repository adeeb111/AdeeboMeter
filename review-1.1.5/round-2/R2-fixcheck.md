# Round 2 fix check — a0fbd56 against round-1's 244 findings

Reviewed commit: a0fbd56 (dev-1.1.5, detached). Round 1 was f6d5747. Read-only.

Method: I read the three "Review fixes" commits (157e38d, 3a888b4, 5a5ae0d) line by line, plus the later feature commits wherever they touch the same code. Where a fix can be tested without Windows, I re-ran the round-1 repro, and variants of it, against the mono build `build2/r2.dll` by reflection. The harness is `review2/harnessV/V.cs` (sections loss, misc, policy, points, parse, crypto, pcap, mem), plus `M.cs` with `srv.py` (hostile mail server) and `E.cs` (Startup.ExeOf).

- "confirmed" means I ran it on r2.
- "read" means I read the code only.
- Line numbers are at a0fbd56.

Muse column: Muse's round-2 report (`review-1.1.5/round-2/muse.md`) was cross-checked. The column is filled only where Muse's status differs from mine, with who is right and why.

Status counts: FIXED 181 · PARTIAL 45 · STILL PRESENT 15 · N/A 1 · REGRESSED 2 (total 244). Separately, the fixes introduced 7 new bugs (listed after the table).

| # | title | status | where (a0fbd56) | evidence / variant | Muse says |
|---|---|---|---|---|---|
| 1 | iperf3 UDP small -l kills app | PARTIAL | Checks\Toolkit.cs:59 BlockLen, :87 ParseOpts, :255-289 stream thread | Confirmed: `-u -l 4` → 16 (crash gone). Variant still open: `-u -l 1M` → BlockLen 1000000 (and `-l 70000` → 70000). That is over the 65507-byte UDP maximum. Send throws SocketException(MessageSize), and the thread's `catch (SocketException) {}` swallows it, so the run reports about 0 Mb/s as a success. The thread still catches only SocketException/ObjectDisposedException. | |
| 2 | Hostile mail server: memory or endless hang | FIXED (confirmed) | Checks\Protocols.cs:169-230 `Lines` (1 KB per line, 20 s for the whole check, Stop) | srv.py against r2: endless `250-` lines → 91 ms, 17 MB; no CRLF → stopped at 20 s, 42 MB; 1 byte per 5 s → 25 s; new variant, a STARTTLS handshake dripped 1 byte per 6 s → stops at 8 s ("TLS handshake took more than 8 s"). Leftover nit: the Offers row holds 2,433 characters (50 caps × 200), no wrap limit. | |
| 3 | Corrupt points.txt throws in the ctor | FIXED (confirmed) | Points.cs:60-61 | `mtr\|9223372036854775807\|-\|2` is skipped, Total=2, no throw. | |
| 4 | Nameless custom game breaks Gamer Zone | FIXED (read) | Gamer\Gamer.cs:107; Ui\FullView.Gamer.cs:96 (GetNextTextElement) | The name falls back to the id; the tile icon takes the first text element (emoji-safe). | |
| 5 | Malformed keys-ec.txt breaks My keys | FIXED (confirmed) | Checks\EcKeys.cs:46-48, Save via tmp + ReplaceFile | Lines `!!notbase64` and `AAAA` are skipped; All() → 0 items; MyKeys → "none yet". The write is atomic, apart from the ReplaceFile fallback (see new bug N3). | |
| 6 | Wi-Fi/advisor parsers throw | FIXED (confirmed) | Link\LinkInfo.cs:290 Randomised TryParse; :434 Util TryParse+clamp; Knowledge\Explain.cs:115 bound | `Randomised("zz:11:22")` → False, no throw. | |
| 7 | Out-of-range history row breaks heat map/Replay | FIXED (read) | History.cs:24 OneDay, :121/:136/:140 range checks; FullView.Replay.cs:49 | Rows outside 0-24 h are dropped at load; a read failure shows an empty day. | |
| 8 | CLI flags crash / case-sensitive | FIXED (read) | Program.cs:139-160 | --write-icon has a try/catch and returns 2; every flag compares OrdinalIgnoreCase. | |
| 9 | Server-name collision after 30-char cut | FIXED (read) | Gamer\GameCompare.cs:69 GroupBy(Name) | Duplicate names are measured once instead of throwing. Duplicates are still not de-duplicated on save, but nothing crashes. | |
| 10 | Corrupt aaa-runs.txt breaks RADIUS lab | FIXED (read) | Checks\EngPack.cs:323 When() | TryParseExact with "(time not readable)". | |
| 11 | Gamer widget card null target | FIXED (read) | Ui\Theme.cs:303 | `if (t == null) return Draw.Card(...)`. | |
| 12 | Paint watchdog crash on close | FIXED (read) | Ui\FullView.cs:70-73 | Checks Disposing and catches Exception. | |
| 13 | Exe-dir policy overrides ProgramData | FIXED (confirmed) | Policy.cs:36-47, 54-66 | Admin file alone: everything off, simple, 3 days. Add an empty exe-dir file: the same. Add an exe-dir file with `mode=pro, history 30, internet on`: the same (flags are OR'ed, min of the days). One residual (pre-existing): an admin file over 16 KB is ignored entirely; confirmed, a 17 KB file with a long comment header gives all-off flags. | |
| 14 | `mode = pro` turns Pro on and locks | FIXED (confirmed) | Policy.cs:66 | `pro` is ignored; Cfg.Mode stays simple. | |
| 15 | Speed `check` ignores speed_test=off | FIXED (confirmed) | Checks\SpeedTest.cs:107 | check and list both return "turned off by your IT team". | |
| 16 | Decrypt code kept in last result, unmasked | FIXED (read) | Ui\FullView.Toolbox.cs SecretInput (+"Code"); Program.CopySecret | The decrypt label starts with "Code", so it is masked and not kept. Generated codes are copied with the Win+V/Cloud exclusion formats. | |
| 17 | Inspect certificate imports PFX key | FIXED (read) | Checks\CertTools.cs:89 | Only `GetCertContentType == Cert` is constructed. | |
| 18 | Capture leftovers survive crash/logoff | FIXED (read) | Program.cs:281-287 CleanCaptures (recursive); AppCtx.cs:281 SessionEnding | Recursive *.pcap* sweep at start and at logoff. The SessionEnding handler isn't marshalled to the UI thread (new bug N5). | |
| 19 | Redaction breaks HTML, misses escapes, keeps LAN names | PARTIAL | Gamer\Gamer.cs:613-625 Redact; Checks\IspReport.cs:83, :296 | Confirmed fixed: rows are redacted before HTML escaping; `td`/`ms` no longer eat tags or units; AT&T works; %zone, 100.64/10 and EUI-64 are caught; private hops show their address only. Variants still open (confirmed): (a) an SSID of 2-3 characters is redacted only when quoted or right after "Wi-Fi ", so `Wi-Fi: Bob` and `On ATT (channel 6)` stay readable; (b) a 4+ character SSID that is a common word ("Router", "Home", "loss") replaces every occurrence of that word in the report ("[Wi-Fi name] ([internal address])" for "router (…)"), which gives the name away by elimination, the same class as round-1's "ms". | |
| 20 | Uninstall deletes key index/backups | FIXED (read) | Setup.cs:208-225 keeps keys-ec.txt, recipients, certs; CertKeys.OutDir → Documents\AdeeboMeter | Mitigated. Keys are still deleted with no "ask before" step, but the index is kept. | |
| 21 | Cert commands: PowerShell injection via smart quotes | FIXED (confirmed) | Checks\CertTools.cs:311 PsSafe, :340 single-quoting | Smart quotes, `\\server`, an apostrophe, backtick, `$(`, U+FF07 and U+2018 are all refused. U+02BC passes, but PowerShell doesn't treat it as a quote. | |
| 22 | Router ping Off ignored with view open | FIXED (read) | Monitor.cs:216 | `Cfg.GwSec == 0 ? 0 : ...`, and the same for Wi-Fi and apps. | |
| 23 | Settings paint rewrites live intervals | FIXED (read) | Program.cs Cfg.JobsForPreset; FullView.Settings.cs JobsFor | Pure computation; Cfg isn't touched. | |
| 24 | Traffic Lab accepts broadcast/multicast/any | PARTIAL | Checks\Lab.cs:68 NotOne | Confirmed refused: 255.255.255.255, 224/4, 0/8, ::, ff02::1, this network's broadcast. Variant (confirmed): IPv4-mapped forms `::ffff:255.255.255.255`, `::ffff:224.0.0.251` and `::ffff:0.0.0.0` return "" (allowed). NotOne checks IPv6 only for IsIPv6Multicast. A dual-mode socket would put them on the wire as IPv4 broadcast/multicast (delivery suspected). | |
| 25 | UDP 10/s cap bypassed on closed port | FIXED (read) | Checks\Lab.cs:191 | The gap sleep always runs after the receive window. | |
| 26 | Security checkup bypasses company gate | FIXED (read) | Checks\SecChecks.cs:413 (Company check before `DhcpOptions(m,"yes")`), :421/:438/:450 router probes | On a company network both DHCP and router probes are skipped. The hard-coded "yes" is only reached when `Lab.Company()` is false, and that is exactly when `Lab.Allowed` would pass without the tick anyway. (Company() itself is still a heuristic: #127.) | Muse: "SecondDhcp still bypasses the gate". **Me.** Muse saw the literal "yes" but missed the `if (Lab.Company(m)) return …` on the line before it. |
| 27 | Machine-store interception roots not flagged | FIXED (read) | Checks\SecChecks.cs:214-225 | LocalMachine roots are matched against Interceptors (amber, or info on a company PC). | |
| 28 | Files planted next to a downloaded exe | PARTIAL | Program.cs:74-100 InDownloads, Portable; Setup.cs:70 ShouldOffer | Fixed: the portable marker and settings beside an exe in Downloads are ignored (WriteDir → %LOCALAPPDATA%). Still open: ShouldOffer still checks `Program.Dir\AdeeboMeter.settings`, so a planted settings file in Downloads still suppresses the install offer. Not covered: the Desktop, or a zip extracted to `Downloads\AdeeboMeter-1.1.5\` (a subfolder isn't "Downloads"). | |
| 29 | Release dispatch builds a different commit; "anyone can check" | PARTIAL | .github\workflows\build.yml:30 checkout ref; build_exe.cmd attestation hint | The dispatch now checks out the tag, and the attestation is documented. Still open: the Store bundle isn't attested. New since round 1 (Hall of Fame, a0fbd56/670f9a3): official builds compile a secret HOF_KEY in, so the workflow header (build.yml:1-2, "so anyone can check the download matches the code") can no longer hold even in principle. | |
| 30 | Widget over presentations / in screen share | FIXED (read) | AppCtx.cs:318 Native.Presenting; Ui\Controls.cs:38 WDA_EXCLUDEFROMCAPTURE | | |
| 31 | Crafted key backup injects keys-ec.txt lines | FIXED (confirmed) | Checks\EcKeys.cs:186-191 strict parse, SafeName | `3\|a\r\nec\|evil…`, `nobar`, `-5`, `99999` → "this key backup is damaged". Nit: SafeName keeps U+202E (Format characters, a bidi spoof in the "My keys" name): SafeName("a\r\nec\|evil‮") = "aecevil‮". | |
| 32 | Decrypt reads the file twice | FIXED (confirmed) | Checks\FileCrypt.cs:129-158 one pass (LimitStream feeds the HMAC) | Bit flips every 997 bytes: 0 accepted; padding-block flip and truncation → the MAC error. | |
| 33 | Half-decrypted file left; Stop does nothing | PARTIAL | Checks\FileCrypt.cs Temp()/Drop()/StopStream | Fixed: Stop works; the temp is hidden `.<guid>.part`; output is renamed only once it checks out. Still open: a kill, crash or logoff mid-decrypt leaves the hidden plaintext `.part` (OneDrive syncs hidden files). There's no DeleteOnClose and no startup sweep. | |
| 34 | Decrypted names: bidi spoof, device aliases | FIXED (confirmed) | Checks\FileCrypt.cs:211-225 | `report‮fdp.exe` → `reportfdp.exe`; `CON .txt`, `COM¹`, `CONIN$`, `conout$.log` → `opened-file-…`. Executables are flagged. | |
| 35 | "Checked out" overstates; .p7m no integrity | FIXED (read) | Checks\FileCrypt.cs Decrypt rows | "not changed or damaged", "Who sent it: not proven", ".p7m: Tamper check none". | |
| 36 | speedservers.txt → localhost/router/metadata | PARTIAL | Checks\SpeedTest.cs:69-81 SafeUrl | Confirmed blocked: localhost, 192.168/16, 127/8, 169.254, 0x7f000001, [::1], fd00::/8, fe80::/10, 100.64/10, `10.0.0.1.`. Variants allowed (confirmed): `http://[::ffff:127.0.0.1]/` and `http://[::ffff:192.168.1.1]/` (Watch.IsPublic misses v4-mapped); names that resolve to private addresses (`127.0.0.1.nip.io`, `localtest.me`) and router vendor names (`router.asus.com`) pass the dotted-name rule. Overriding a built-in id no longer works. | |
| 37 | Non-ring Sniff promiscuous | FIXED (read) | Ui\FullView.cs Sniff `-p` | | |
| 38 | Discovery binds UDP sockets (rule 2) | STILL PRESENT | Checks\Reach.cs:210 | Unchanged; still waiting on the owner's decision. | |
| 39 | Reverse lookup extra forward query; hung thread | PARTIAL | Native.cs:94 PtrName (DnsQuery PTR only), used everywhere | The forward query is gone. Hung resolver tasks are still abandoned after Wait(3000), not cancelled. | |
| 40 | RADIUS accepts Accept without Message-Authenticator | FIXED (read) | Checks\Protocols.cs ReplyOk(out hasMa); EngPack.cs:300 | An amber BlastRADIUS row. | |
| 41 | OpenUrl allowlist prefixes | FIXED (read) | Brain\Watch.cs Crash.Allowed (Uri parse, host + segments) | Self-test covers AdeeboMeterEvil, @evil and userinfo. | |
| 42 | Bidi/zero-width in SSIDs, process metadata | FIXED (read) | Program.Plain used in LinkInfo, Monitor, Explain | Nit: not applied to key names (#31). | |
| 43 | Version still 1.1.4 | STILL PRESENT | Program.cs:113-114 | Known; not dwelt on. | |
| 44 | MTR/ISP blame destination-only loss | FIXED (confirmed) | Checks\PathTools.cs:123-131 | Destination 10% → -1, "Loss at the destination only (10%)"; destination 50% → the same. The self-test now asserts it. A related regression came from #59's fix (new bug N1). | |
| 45 | URL target breaks ping/trace; VPN guide green | FIXED (confirmed) | Checks\Toolkit.cs:33-49 HostOnly/UrlToTarget; Icmp.cs:97; Investigate.cs:253-262 | `https://127.0.0.1/status` → 127.0.0.1; userinfo, IPv6 with port and ports all fine; the VPN door verdict no longer depends on MtuAt. Typo nit: `https:/example.com` → host "https". | |
| 46 | Non-installed copy takes over Start with Windows | FIXED (read) | AppCtx.cs:40, :229-236; Setup.cs:58 OurRunValue | The default is off unless installed; a copy changes only its own entry; Uninstall removes any AdeeboMeter* Run value. (The shared mutex and keys for portable copies remain.) | |
| 47 | GwEverReplied never reset | FIXED (read) | Monitor.cs:256 netChanged clears it; :742 link logic | | |
| 48 | Wall-clock scheduling | FIXED (read) | Program.cs Clock.Mono; Monitor, AppCtx, Gamer, Watch.Pinned | No mixed UtcNow/Mono comparisons found. | |
| 49 | False ARP-spoofing alarm across networks | FIXED (read) | Monitor.cs:485-500 (learn on a new key, amber on one change, red only on an A-B-A flip within 10 min) | | |
| 50 | "Router doesn't answer" without testing internet | FIXED (read) | Checks\Investigate.Whats.cs:86-118 | | |
| 51 | Captive portal blamed on provider | FIXED (read) | Investigate.Whats.cs:98-106 PortalTest before step-4 verdicts | | |
| 52 | Speed test A+ when loaded pings time out | FIXED (confirmed) | Checks\SpeedTest.cs:96-100 GradeOf, :258 timeout=1000 | GradeOf(20,NaN)="" ; (NaN,50)=""; (20,1000)="F". | |
| 53 | Ctrl+Alt+A eats AltGr+A | FIXED (read) | Ui\Controls.cs:452; Native.cs AltGrTypes | The hotkey is skipped where any layout types with AltGr+A. It is still not configurable. | |
| 54 | Find a MAC / IP to MAC past 2,000 entries | PARTIAL | Checks\SnmpGear.cs:91-104 (MAC: direct GET + "may still be here"); :75-77 (IP) | Find a MAC is fixed. IP to MAC still walks the ARP table with the 2,000 cap and answers "this device doesn't have it in its ARP table" when the table was cut. Vlans/Lag views are unchanged. | |
| 55 | Painted content invisible to keyboard/screen reader | PARTIAL | Ui\FullView.cs WireKeys; Controls.cs TabStep/Press/Announce | Tab/Enter now reach painted acts. Most are announced as "item N of M"; values, verdicts and path dots still have no accessible objects. | |
| 56 | Offline/no-gateway wrong verdicts | PARTIAL | Monitor.cs:250 (no DNS probe offline), :745 offline verdict | Cases A and B are fixed. C (IPv6-only: Primary null → "No network connection") is not handled. | |
| 57 | --report exits 0 when a step couldn't run | FIXED (read) | Report.cs: lv 0 → 3 if any observation is 3; exit 4 for I/O; catch all | | |
| 58 | ISP report: provider's 10.x/CGNAT hop blamed on home | FIXED (read) | Checks\IspReport.cs:90-96 (boundary = the hop after m.Gateway) | Needs a Monitor instance to run; LossStart for the case gives hop 2, and firstIsp is now hop 2 by reading. | |
| 59 | Outage past hop N = "No real loss" | REGRESSED | Checks\PathTools.cs:121 Answers | The exact repro is fixed (confirmed: → hop 3, 100%), but the new `Answers` rule breaks the case that was right at f6d5747: a quiet tail hop that answered the trace now makes an earlier hop's rate-limit loss "real loss to the end" (new bug N1, confirmed). | |
| 60 | Outage log never closes on a network change | PARTIAL | Brain\Outages.cs:35-45; Monitor.cs:253, :255 | Confirmed: the round-1 Pair input now gives "18:00 · end not seen" plus two correct outages. Variant: the new level-0 "Network changed while the internet was quiet" and "Internet check switched off…" events are taken as "back", so the outage shows as ended at that moment with a made-up length (new bug N2). "From" is still the 3rd failure; sleep is still counted. | |
| 61 | pcapng ns timestamps 1000x off | FIXED (confirmed) | Checks\Pcap.cs IDB option 9 | The DHCP replay shows 1.000/2.000/3.000 s; "Lease … in 3.000 s". | |
| 62 | >20 P-384 recipients → unopenable | FIXED (read) | Checks\EcKeys.cs:116-118 | De-duplicated; 1-20 enforced at encrypt. | |
| 63 | Store build: backups virtualised | FIXED (read) | Checks\CertKeys.cs:29-36 OutDir = Documents\AdeeboMeter | | |
| 64 | Amber observations end green | FIXED (read) | Checks\Investigate.cs:94-99; Whats thresholds 15 ms / 150 ms / 1% | | |
| 65 | Monitor English-only netsh labels | PARTIAL | Monitor.cs:405-430 ParseWifi | Confirmed fixed: French (NBSP %), Spanish, Italian, German. Still open (confirmed): Turkish `Sinyal : %77` → sig -1 (the regex wants the % after the number); channel/state empty for Chinese, Russian, Polish ("Kanał") and Japanese (no keys). HasWifi now comes from the adapter, so the dot stays Wi-Fi, but channel-change/drop events are missing on those locales. | |
| 66 | Wi-Fi page "great shape" on no data | FIXED (read) | Link\LinkInfo.cs:460-475 ParseWifi fallback, WlanApi band, unknownAir | | |
| 67 | Health check "channel calm" non-English | FIXED (read) | Checks\HealthCheck.cs:76-84 WlanApi | | |
| 68 | Saved Wi-Fi green non-English | FIXED (read) | Checks\SecChecks.cs:333-338; LinkInfo WlanApi.Profiles (XML) | | |
| 69 | Esc closes the view from text boxes | FIXED (read) | Ui\FullView.cs:316-320 | | |
| 70 | Tabs/bars clip | FIXED (read) | Ui\FullView.cs FitBars (wrap) | Not rendered on Windows. | |
| 71 | Lab builders/scanners earn points | FIXED (confirmed) | Points.cs:21-23 | icmpb/arpwho/dhcpopt/manyports/sweep/finddev → 0; the replay set no longer has speedtest/iperf/bloat. (twamp/udpmtu/radiuslab/smtppath still earn, but they're outside the gate: #229.) | |
| 72 | Failed points read wipes total | FIXED (read) | Points.cs loadFailed + reload before save | The ReplaceFile fallback can still lose the file (new bug N3). | |
| 73 | +2 replay beaten by moving the clock | PARTIAL | Points.cs:108-111 | Confirmed in-session: clock +11 min, mono +1 → 0. Variant (confirmed): run MTR, quit, move the clock +11 min, restart, run again → bonus paid (firstMono is per session). | |
| 74 | "Use a page" pays for opening the tab | PARTIAL | Ui\FullView.Points.cs:201-211 WatchUse | It now needs a MouseDown or KeyDown anywhere on the page, so one click on empty space (or on the scrollbar) still earns +2 per page. | |
| 75 | Self-test writes fake history | FIXED (read) | SelfTest.cs:91 Cfg.History/Alerts/Portal off | | |
| 76 | Examples point at real hosts | FIXED (read) | knowledge\examples.txt (192.0.2.x, example.*); FullView.Examples.cs NetBoxWords check | gamecompare "valorant" still pings real game servers, by design. | |
| 77 | "Own words" → wrong guide | PARTIAL | Ui\FullView.Overview.cs:121-157 | Confirmed: all six round-1 phrases are now right. Variants (confirmed): "my work laptop is slow" → VPN guide (should be pc/slow); "the network at work is slow" and "youtube buffering at work" → VPN guide (the "at work" rule ranks above slow/buffering); "streaming works but games lag" → slow (stream before game); "I can't get to work email" → nothing. | |
| 78 | THE PC examples unreachable; Go runs a disk scan | FIXED (read) | Ui\FullView.Toolbox.cs:216 OpenTool → SelectTool; FullView.Points.cs:63 | | |
| 79 | Two games with the same id replace each other | FIXED (read) | Ui\GameEditor.cs:104-113 | A duplicate name is refused; the id gets a suffix; ApplyFile accepts `my-x-2`. | |
| 80 | GameEditor quoted path | FIXED (read) | Ui\GameEditor.cs ExeName | | |
| 81 | VPN awareness misses products / PPPoE | FIXED (read) | Monitor.cs:282 vpnWords, :285 PPPoE; VpnAware.cs:52-59 route check | | |
| 82 | Match server = PC's own public IP | FIXED (confirmed) | Gamer\Gamer.cs:665-690 | → 198.51.100.20. | |
| 83 | Speed test measures redirects | FIXED (read) | Checks\SpeedTest.cs:146-151, :245-249 | | |
| 84 | Only 2 of 4 streams; time base | FIXED (read) | Checks\SpeedTest.cs:231 ConnectionLimit, :260 secs before Join | | |
| 85 | Stop during upload saved as complete | FIXED (read) | Checks\SpeedTest.cs:196 | The row then says "Nothing else was sent", which is wrong after a partial upload (cosmetic). | |
| 86 | SMTP path: bad cert shown "checked"; dead port-25 hint | FIXED (confirmed) | Checks\EngPack.cs:328-345 HowFar, :362 | Bad cert → "certificate FAILED"; greeting then timeout → "stopped (…)". | |
| 87 | Cert compare never on non-443 | FIXED (confirmed) | Checks\EngPack.cs:32-36 | `host:8443`, `[v6]:8443` → parsed. | |
| 88 | RADIUS >128-byte password malformed | FIXED (read) | Checks\EngPack.cs:268 refuses >128 | HidePassword itself still produces 144 for 129 (confirmed), but the lab guards it. | |
| 89 | "Slower than usual" after switching networks | FIXED (read) | Brain\Brain.cs:221 recentKey per network; ≥4 samples | | |
| 90 | History compaction overwrites .gz | FIXED (read) | History.cs:68-77 merge; Lines reads both | | |
| 91 | Alt+F4 mid-install | FIXED (read) | Setup.cs:274 OnFormClosing | | |
| 92 | Read-only folder: endless offer, no settings | FIXED (read) | Program.cs:69-90 WriteDir fallback | | |
| 93 | Older exe "updates" the newer install | FIXED (read) | Setup.cs:296-298 Newer | | |
| 94 | Renamed copies escape StopOthers | FIXED (read) | Program.cs Single.Others (prefix + session), RunningIsOlder | | |
| 95 | Writers and readers lock different objects | FIXED (read) | Brain\Watch.cs lock(Summaries/Joined); Insights copies; Pinned reads once | Nit: MoreTools.cs:205 (MySites, on a worker thread) still enumerates Cfg.Pins unlocked. | |
| 96 | SNMP walk accepts backwards/looping OIDs | FIXED (confirmed) | Checks\Snmp.cs:373 OidCompare | OidCompare unit-checked; the walk stops with "OID not increasing". | |
| 97 | 32-bit counter reset → phantom rate | REGRESSED | Checks\SnmpTools.cs:171-180 | The phantom rate is gone, but the wrap is now accepted only when ifHighSpeed (ifXTable) is known. The 32-bit path (`hc=false`) is taken exactly on devices without ifXTable, so `hs` is empty there, port=0, and every real wrap shows "reset" (new bug N6). It should fall back to ifSpeed (2.2.1.5). | |
| 98 | IPv6 vs IPv4 "Neither connected" green | FIXED (read) | Checks\PathTools.cs:226-235 | All combinations; level 2 when neither connects. | |
| 99 | DHCP client counted as server | FIXED (read) | Checks\RoutingTools.cs:161 server-id | | |
| 100 | What's wrong DNS step: DoH / second server | FIXED (read) | Investigate.Whats.cs:124-145 | | |
| 101 | Averaged ring data hides link down now | PARTIAL | Investigate.Whats.cs:49-51 | Retests when the newest sample is NaN. Samples minutes old after sleep or a slow NetSec are still trusted (no timestamps). | |
| 102 | MtuAt calls rate limiting a black hole | FIXED (read) | Checks\PathTools.cs:45-52 third try after 1.1 s | | |
| 103 | 802.1X successes counted as failures | FIXED (read) | Checks\WinEvidence.cs:91-92 | | |
| 104 | Look-alike false alarms | FIXED (read) | Link\WifiAdvisor.cs:110-122 | | |
| 105 | Live capture "capturing" after tshark dies | FIXED (read) | Capture\LiveCapture.cs:170 exit, :178 limit timer, ring stderr | | |
| 106 | Policy overwrites user settings | FIXED (confirmed) | Policy.cs:79-92 mine/Keep; Program.cs:349-354 | NetSec effective 0, saved `net=60`, `mode=simple` (the user's own). | |
| 107 | P-384 index travels without key | FIXED (read) | EcKeys.Key.Here; CertKeys MyKeys; FileCrypt "also for me" uses Here | | |
| 108 | No warning when "also for me" has no P-384 key | FIXED (read) | Checks\FileCrypt.cs Encrypt; CertKeys.cs:277-299 key-change warnings | | |
| 109 | Import wipes pins first; widget/theme ignored | FIXED (read) | Ui\FullView.Settings.cs:520-545 | | |
| 110 | Delete history one click | FIXED (read) | Ui\FullView.Settings.cs:233-238 MessageBox, No default | | |
| 111 | System-aware DPI only | STILL PRESENT | Program.cs:130 SetProcessDPIAware | No per-monitor manifest. | |
| 112 | Cover zone fixed 264x300 | FIXED (read) | AppCtx.cs:300-309 Zone ∪ Placed | | |
| 113 | Touchpad scrolling | FIXED (read) | Ui\Controls.cs wheelRest | | |
| 114 | Held Enter repeats toggles | FIXED (read) | Ui\Controls.cs held flag | Nit: any KeyUp (Shift) re-arms it. | |
| 115 | Second launch opens behind | FIXED (read) | Program.cs AllowSetForegroundWindow(ASFW_ANY) | | |
| 116 | Search ignores the user's language | FIXED (read) | Ui\FullView.Toolbox.cs:728-731, :753; lang-fr "Did you mean", "Nothing matches" | | |
| 117 | Deep test hits the company DC | FIXED (read) | Ui\FullView.DeepTest.cs:24 ldapcheck, shares | | |
| 118 | TLS 1.3 "not offered" on Win10 | FIXED (read) | Checks\Toolkit.cs:688 | | |
| 119 | dig silently falls back | FIXED (read) | Checks\Toolkit.cs:399-409 | | |
| 120 | IDN as '?' | FIXED (confirmed) | Checks\Toolkit.cs:483 IdnMapping; Reach.cs:138 | The Question label is `xn--bcher-kva`. | |
| 121 | Hash compare fails on pasted formats | FIXED (confirmed) | Checks\CertTools.cs:129-137 | "SHA256: …", certutil spaced pairs, `sha256:…`, `{…}`, `hash  f.txt` → Matches. | |
| 122 | Subnet bad prefixes → /32 | FIXED (confirmed) | Checks\Toolkit.cs:834-850 | `/-1`, `/abc`, `/8 foo`, non-contiguous, IPv6 mask, `1.2.3/24`, `/ 8` → errors. | |
| 123 | Scoped IPv6 literal loses port | FIXED (confirmed) | Checks\DestCheck.cs:70 | `[fe80::1%12]:8443` and `%eth0` → 8443. | |
| 124 | Smaller parsing slips | PARTIAL | Checks\DestCheck.cs:74; Protocols.cs:31,66; CertTools JWT; MoreTools ports/MySites/Rdp/Ldap; Reach headers/body; Toolkit dig | Read-fixed: http prefixes, host:abc, JWT ms, 100000, MySites, Rdp, header `;`, GET body, dig TCP, Ldap. Still open (confirmed): DestCheck.Parse accepts `1.2.3`, `0x7f.1`, `4294967295` and `01.02.03.04` (True, host kept). The new strict IPv4 regex is bypassed because these fall into the host-name branch, and Windows' resolver then reads them as numeric IPs (1.2.0.3, 127.0.0.1, …). | |
| 125 | IPv4-only TcpClient | FIXED (read) | Checks\Toolkit.cs:23-30 Tcp() dual mode; brackets stripped | | |
| 126 | BitLocker values | FIXED (read) | Checks\SecChecks.cs:110-114 | 5 = suspended (amber), 6 = on. | |
| 127 | Lab company guard fails open | FIXED (read) | Checks\Lab.cs:29-50 CloudManaged; "doesn't look like a company network" row | | |
| 128 | Windows Update age culture | N/A | Checks\SecChecks.cs:133 | Kept invariant on purpose. WMI InstalledOn really is M/d/yyyy whatever the locale, so the round-1 finding was likely wrong. | |
| 129 | SNMP "n/a, never 0" | FIXED (read) | Checks\Snmp.cs HasNum/Signed, ReadPdu len<1; SnmpGear Gl/Sn/FDB; SnmpTools precision | | |
| 130 | Overlap flags full-tunnel 0/1+128/1 | FIXED (read) | Checks\PathTools.cs:198-207 | | |
| 131 | RouteFor no route green | FIXED (read) | Checks\RoutingTools.cs:123 | | |
| 132 | w32tm error green | FIXED (read) | Checks\SysTools.cs:329-331 | | |
| 133 | TeamsUdp NAT with one server | FIXED (read) | Checks\WhyFails.cs:151-178 | | |
| 134 | Interfaces delta reboot/renumber | PARTIAL | Checks\SnmpTools.cs:99, :136 | Renumbering is caught by name. With sysUpTime unreadable (upt=0), a restart still shows "+N". | |
| 135 | Localized command output | FIXED (read) | Checks\WhyFails.cs:58-68 WinHTTP API; SysTools.cs:160-189 gpresult /x | | |
| 136 | "Public CA" by name substring | PARTIAL | Checks\Inspection.cs:57-68 | It now needs a self-signed root and an O= match, but it is still a substring of a name the root's maker chooses. A malicious root `CN=Proxy, O=DigiCert Inc` (self-signed) counts as public, so inspection is reported as absent. Not by thumbprint/CTL. | |
| 137 | SNMP community/v3 parsing | FIXED (read) | Checks\Snmp.cs:33-37 `community:`, UTF-8, port-unreachable, accept() | | |
| 138 | Linux cache counted as used | FIXED (read) | Checks\SnmpTools.cs:212-219 | | |
| 139 | Listening view ignores IPv6 | FIXED (read) | Checks\SysTools.cs:269-273 | | |
| 140 | Startup list: illegal path char hides entries | FIXED (confirmed) | WindowsDeep\Inside.cs:190-197 Startup.ExeOf | Stray quote, tab, `\|`, `<>` → x.exe/evil.exe, no throw; the per-value read is caught. | |
| 141 | M365.Role by substring | FIXED (confirmed) | Apps\M365.cs:30-44 | All three hostile names → ""; real names keep their roles. | |
| 142 | 6 GHz reported as 2.4/5; negative channels | PARTIAL | Link\LinkInfo.cs:231 FromFreq; :450 API band | 5935 → ch 2 and the API band is used. Overlap still ignores channel width (bonding). | |
| 143 | DHCPCAPI stride on x86; no cleanup | FIXED (read) | Checks\Lab.cs:288-311 | | |
| 144 | Process tree PID reuse | FIXED (read) | WindowsDeep\Inside.cs:24-64 | | |
| 145 | "on battery 255%" | FIXED (read) | WindowsDeep\PcHealth.cs:57-58 | | |
| 146 | Two sniffs share a folder | FIXED (read) | Capture\LiveCapture.cs:385-387; FullView Sniff file name + Guid | | |
| 147 | tshark "valid" disables extras | FIXED (read) | Capture\LiveCapture.cs:107 | | |
| 148 | Smaller capture/Wi-Fi slips | PARTIAL | FullView Sniff zone strip; PcHealth battery file; LinkInfo ActiveBlock; netsh OEM | Fixed: fe80 filter, temp file, two adapters, OEM. Still open: WifiAdvisor.cs:95 still suggests 149-165 (165 is 20 MHz-only and restricted in some regions); Packets.cs:38 dead null check. | |
| 149 | NBSP before % | FIXED (confirmed) | Link\LinkInfo.cs:419,446; Monitor.ParseWifi | `85 %` → 85. | |
| 150 | dumpcap child escapes Job; Stop2 kills tshark only | PARTIAL | Capture\LiveCapture.cs:101, :145-150 | Stop2 now kills child dumpcap by parent PID. Process.Start then Job.Add is still racy (not started suspended). | |
| 151 | Upgrade re-enables autostart | FIXED (read) | Setup.cs:262 InstalledAutoStart | | |
| 152 | Ages/dedupe/outage lengths on wall time | FIXED (read) | Model.cs Ev.Mono, Ago clamp; Outages.O.Len clamp | | |
| 153 | Crash URL too long | FIXED (read) | Brain\Watch.cs IssueUrl ≤ 7,000 | | |
| 154 | Adapter choice ignores metric / IPv6-only | PARTIAL | Monitor.cs:291-305 Routes.WindowsPick(1.1.1.1) | The IPv4 best route is used. IPv6-only (no IPv4 gateway) is still not chosen. | |
| 155 | File.Replace fails on SMB/USB | FIXED (read) | Program.cs:211 ReplaceFile fallback | The fallback itself is risky (new bug N3). | |
| 156 | Usage/first-seen app logic | FIXED (read) | Brain\Brain.cs:117, :25; Watch.cs Unversioned, GwAnswering | | |
| 157 | Install pattern may trip Defender | PARTIAL | Setup.cs:143-148 deletes Zone.Identifier | MOTW is stripped on the installed copy. The pattern itself is untested. | |
| 158 | Paths > MAX_PATH fail silently | FIXED (read) | Program.cs CanWrite (dir > 200 → AppData) | | |
| 159 | SystemEvents off the UI thread | PARTIAL | AppCtx.cs:137-139 theme marshalled | Fixed for UserPreferenceChanged. The new SessionEnding handler (AppCtx.cs:281) isn't marshalled (new bug N5). | |
| 160 | SHA256SUMS CRLF | FIXED (read) | release.ps1:54 | | |
| 161 | Save/clipboard failures silent | FIXED (read) | Ui\FullView.Tools.cs CopyFailed / Save MessageBox | | |
| 162 | Changes chart not redrawn | FIXED (read) | Ui\FullView.Paint.cs:291 key includes the histData identity | | |
| 163 | Widget re-claims TOPMOST over tray menu | PARTIAL | Ui\Controls.cs:40, :60 HoldTop; AppCtx.cs:100 | The tray menu is fixed; nothing for an auto-hide taskbar. | |
| 164 | Quit under a modal disposes the view | PARTIAL | AppCtx.cs:292 | `f.Close()` on a modal Form only sets DialogResult. ShowDialog returns after this handler has already run `full.Dispose()`, so code after ShowDialog still runs on a disposed view. Save/Export are CommonDialogs, which aren't in Application.OpenForms, so they aren't closed at all. | |
| 165 | Text boxes no name; PillButton names stale | PARTIAL | Ui\Controls.cs Cue→AccessibleName, OnTextChanged, Selected description | Names are fixed. Badges ("3 new", "REC") aren't exposed; disabled Primary contrast is unchanged. | |
| 166 | Tab order reversed | FIXED (read) | Ui\FullView.cs:300-303 TabIndex | | |
| 167 | High contrast ignored; colour-only status | FIXED (read) | Ui\Theme.cs:68-75 SystemColors; Draw.LvDot glyphs | | |
| 168 | RSA PFX backup hint wrong | FIXED (read) | Checks\CertKeys.cs:347 | | |
| 169 | Store startup switch reads "off" | FIXED (read) | AppCtx.cs:425-426 missing → on | | |
| 170 | --report dns null | FIXED (read) | Report.cs DnsMs from "5. Names" | | |
| 171 | Language switch / example day | FIXED (read) | FullView.Settings.cs:101 "after a restart"; FullView.Replay.cs:115 Make(replayDay) | | |
| 172 | 63 MB pcap → ~175 MB managed | STILL PRESENT (confirmed) | Checks\Pcap.cs:37 ReadAllBytes; Wanted() filter | The round-1 repro (300-byte DHCP-like frames) passes the new filter, so 184,526 packets and +120 MB retained on top of the 63 MB array. The filter helps only for non-DHCP traffic. | |
| 173 | French Live-capture button repaint loop | FIXED (read) | Ui\FullView.cs:531 compares with L.T(...) | | |
| 174 | Paint re-spawns loaders on failure | FIXED (read) | FullView.Travel.cs:51-59 one at a time, 60 s back-off; Insights 1 min | | |
| 175 | HTTP builder hangs for days | FIXED (read) | Checks\Reach.cs:75-79 20 s, Run.Stop | | |
| 176 | Sniff captures no size cap | PARTIAL | FullView Sniff and LiveCapture forward `-a filesize:200000` | Each capture is now capped at 200 MB. Ring copies plus the merged file still multiply it; no free-space check. | |
| 177 | WMI disk query no timeout | FIXED (read) | WindowsDeep\PcHealth.cs:274 | | |
| 178 | Stream overlay PNG every second | PARTIAL | Ui\StreamOverlay.cs:108-112 | It is written only on change, but still PNG-encoded every second. The "last 60 s" graph label, opaque PNG, no owner window and untranslated strings remain. | |
| 179 | .p7m holds 200 MB several times | PARTIAL | Checks\FileCrypt.cs:25 MaxCms 50 MB to make, MaxCmsOpen 200 MB | Opening a 200 MB .p7m still does Decode(ReadAllBytes); OOM isn't caught. | |
| 180 | Log: no rate limit, wipes, culture, newlines | FIXED (read) | Program.cs Log | | |
| 181 | Brain.Bg new thread every 5 s | FIXED (read) | Brain\Brain.cs:80-97 | | |
| 182 | AutoScroll oscillation | FIXED (read) | Ui\FullView.Inside.cs SetNeed hysteresis | | |
| 183 | Disk/process I/O in Paint | FIXED (read) | FullView.Settings.cs HistStat (10 s); Inside exePathCache | | |
| 184 | Tray tooltip rewritten | FIXED (read) | AppCtx.cs:376-377 | | |
| 185 | Disk counters block sampler | FIXED (read) | Monitor.cs:121-134 task | | |
| 186 | GPU tool 400 counters | FIXED (read) | WindowsDeep\PcHealth.cs:68-83 ReadCategory | | |
| 187 | SNMP snapshot message; v3 Stop slow | PARTIAL | Checks\Snmp.cs:365 message fixed; :325-330 Localize | The 1 MB password stretch still runs for every message (no per-engine cache). | |
| 188 | PrimaryScreen null floods log | FIXED (read) | AppCtx.cs:300, :316 | | |
| 189 | "On SR OS" (rule 8) | FIXED (confirmed) | Checks\Protocols.cs Twamp hint; SelfTest product-name check | grep over src, knowledge, docs, README, CHANGELOG, store: 0 hits. | |
| 190 | French mixed-language | PARTIAL | knowledge\lang-fr.txt (651 lines) | 140 of 141 tool titles and the search strings are now in French. The Award flow, Neighbour wars and section tabs are still English (agrees with Muse). | |
| 191 | French clipped in fixed rects | STILL PRESENT | Ui\FullView.Overview.cs Fix tiles; Gamer\Gamer.cs 120*S/100*S boxes | Not touched by any fix commit (read). | |
| 192 | Labels measured in English | FIXED (read) | FullView.Paint.cs:87; Theme.cs:321/386/435 | | |
| 193 | Capture status "30 s"; stale label | PARTIAL | Ui\FullView.cs Sniff status uses Cfg.SniffSec | The live-capture "after N min" label is still built once. | |
| 194 | Font hash collides | FIXED (read) | Ui\Theme.cs:236 | | |
| 195 | Detail card/tips not clamped | PARTIAL | AppCtx.cs:376 card; Controls.cs:574 tips | The card and tips are clamped; unclipped DrawString rows remain. | |
| 196 | Trunc cuts surrogates | FIXED (read) | Ui\Theme.cs:250-255 | | |
| 197 | Segoe UI Variable under GDI+; CJK/emoji | PARTIAL | Ui\Theme.cs:213-224 | Bold now uses Segoe UI. Missing-glyph fallback is unchanged. | |
| 198 | Switches expose no checked state | FIXED (read) | Ui\Controls.cs SwitchAccess | | |
| 199 | ConnList keyboard; text scale; widget hover-only | PARTIAL | Ui\Controls.cs:412-436 | Arrow keys and Enter added. Text scaling is still ignored, and the widget is still hover-only. | |
| 200 | Header/self-test overstate privacy | FIXED (read) | Program.cs:1-12; SelfTest Portal off | Muse notes portable usage.txt; minor. | |
| 201 | Remover helper left in %TEMP% | FIXED (read) | Program.cs:150 | | |
| 202 | Small encrypt/decrypt message slips | FIXED (read) | FileCrypt magic detection, kind messages, UTF-8 name cut; CertKeys "code" name; Toolbox pub FreeName | | |
| 203 | Hostile ephemeral key type | FIXED (read) | Checks\EcKeys.cs:149-152 | | |
| 204 | ICMP inner IHL<5 bogus port | FIXED (confirmed) | Checks\Replay.cs:185 | No port is printed; the MTU is read correctly. | |
| 205 | Unsupported link types read as "no DHCP" | FIXED (confirmed) | Checks\Pcap.cs:38-39 FirstLink | "link type (127, Wi-Fi monitor mode) isn't supported". | |
| 206 | privacy.html leaves out traffic | FIXED (read) | docs\privacy.html "What it sends" | Background items now match About. | |
| 207 | "Online lookups only send public IPs" false | PARTIAL | README.md:79,102 fixed; docs\index.html:833, :974 | index.html still says "public IP addresses only → RIPEstat, PeeringDB" (no Security checkup DoH/TLS). | |
| 208 | Header overflows <388 px | FIXED (read) | docs\index.html @media (max-width:400px) | Not re-run in Playwright. | |
| 209 | API unreachable: stale/no answer | FIXED (read) | docs\index.html cantCheck(), version 1.1.5 | | |
| 210 | PageUp/Down hijacked | FIXED (read) | docs\index.html:1052 | | |
| 211 | 880 px gap under slides | FIXED (read) | docs\index.html fit() | | |
| 212 | Contrast fails | FIXED (read) | docs\index.html --blue #2F6FDB, donate #F06292, footer underline | Not re-measured with axe. | |
| 213 | "103 tools" wrong | PARTIAL | README:32, CHANGELOG:7 say 140; docs\THEORY:235 says 129 | 141 are registered (agrees with Muse). | |
| 214 | Stale version strings | FIXED (read) | docs → 1.1.5 | The app itself is still 1.1.4 (#43). | |
| 215 | ?v= spoofable banner | FIXED (read) | docs\index.html version regex; "ahead" | | |
| 216 | privacy.html orphan | FIXED (read) | footer link; sitemap | | |
| 217 | Sitemap stale; no robots.txt | PARTIAL | docs\sitemap.xml updated | docs\robots.txt still missing. | |
| 218 | Slide footer numbers | FIXED (read) | docs\index.html renumber in JS | | |
| 219 | "One readable file" | FIXED (read) | index.html, CONTRIBUTING.md | | |
| 220 | Self-test claims out of step | PARTIAL | index.html "61 checks" | index.html:879 still says "opens in under 1 second" (the test allows 2 s). | |
| 221 | Page claims no remote loads | FIXED (read) | index.html:24 comment | | |
| 222 | No captions; autoplay ignores reduced motion | PARTIAL | index.html:1068 | Reduced motion and saveData fixed; there is still no `<track>`. | |
| 223 | Site keyboard/screen-reader polish | PARTIAL | focus-visible, aria-pressed added | Mobile nav is still hidden under 860 px. | |
| 224 | privacy.html ignores theme | FIXED (read) | privacy.html:29 | | |
| 225 | README says "offline" | FIXED (read) | README:13 "local-first" | | |
| 226 | Suggestion box drops Ideas text | FIXED (read) | index.html `&idea=`; length caps | | |
| 227 | Store section on privacy page | FIXED (read) | privacy.html:75 "If … offered" | | |
| 228 | Docs: advisor works without location | FIXED (read) | THEORY:226; README:29 | | |
| 229 | TWAMP/UDP-MTU/Engineer pack bypass the Lab gate | STILL PRESENT | Ui\FullView.Toolbox.cs:207 LabGate unchanged; :211 self-test scans "lab" sections only | twamp is still capped at 2,000. These tools also still earn points (Value = 2). New dnsrace (147) is outside the gate too (agrees with Muse). | |
| 230 | "Pick from running programs" empty | FIXED (read) | Ui\GameEditor.cs:147 | | |
| 231 | History ignores portable mode | STILL PRESENT | History.cs:17 | | |
| 232 | TCP:443 fallback never re-arms | STILL PRESENT | Monitor.cs:241-242 NetIcmpEver | | |
| 233 | CI never builds dev | STILL PRESENT | .github\workflows\build.yml:9 | | |
| 234 | "printer" opens Ping sweep | STILL PRESENT | Ui\FullView.Toolbox.cs:518; DeepTest.cs:149 | | |
| 235 | No notification for outages | FIXED (read) | Brain\Watch.cs:40 Raise("inetback", "Internet back …") | A pop-up when the internet comes back ("Every outage is listed in Time machine"), plus the existing "went quiet" pop-up at :39. That is what CHANGELOG promises. | Muse: "Outage notification still half-built … no notification call anywhere". **Me.** Watch.cs:40 raises on the "Internet back" event; Muse looked for an outage-specific call in Outages.cs. |
| 236 | Theory says no public speed tests | FIXED (read) | docs\THEORY | | |
| 237 | French help sentence overstates | FIXED (read) | Ui\FullView.Settings.cs:101 | The sentence is now honest, though it understates (Muse). Section tabs are still literals (#190). | |
| 238 | "Same certificate" names the issuer | STILL PRESENT | Checks\EngPack.cs:83 `a.Chain[0][1]` | | |
| 239 | DSCP `100`; headline ignores unmarked loss | STILL PRESENT | Checks\Lab.cs (dscp regex) | Unchanged (agrees with Muse). | |
| 240 | TcpMs leaks wait handles | STILL PRESENT | Gamer\Gamer.cs:478-490 | | |
| 241 | 802.1X replay first 200 frames | STILL PRESENT | Checks\Replay.cs:211 | | |
| 242 | Health tools earn without the Example | FIXED (read) | Ui\FullView.Tools.cs:31 exOpened | | |
| 243 | Leftovers: Cookie redaction, ssl search, dmarc, Security Save | STILL PRESENT | Ui\FullView.Toolbox.cs:689 (Cookie), :517 ("cert"/"ssl" → http first) | No spf/dkim/dmarc words; no Security Save. | |
| 244 | PLAN and privacy page out of step | PARTIAL | PLAN-1.1.5.md fresh; docs\privacy.html | privacy.html still lists none of: game list, keys/recipients, Points, outage log (what it keeps). | |

## New bugs introduced by the fixes

### N1. MTR/ISP report: a hop that answered the trace and then ignores every ping now makes a normal earlier loss "real loss to the end"
Severity: wrong result
Where: src\Checks\PathTools.cs line 121 (`Answers`, changed in 5a5ae0d for #59), lines 123-131 (LossStart), 134-143 (MtrVerdict); src\Checks\IspReport.cs line 209 (StartOfLoss)
How to reproduce (run on r2, `harnessV loss`):
- Hops 192.168.1.1 0%; 203.0.113.1 6/120 lost (rate-limits); 203.0.113.5 120/120 lost, but it answered the trace, so its Ip is set; destination silent (no IP).
- Or the same with hop 2 at 12/120.
What happens:
- LossStart = 1. The verdict reads "Real loss starts at hop 2 (203.0.113.1) and carries on to the end - 5% lost from there. The destination doesn't answer pings at all, so this is judged by the last router that does (hop 3)", although hop 3 answered none of the 120 MTR probes.
- At f6d5747 the same input gave -1 ("No real loss"): the 100% hop was a quiet router.
- The fix treats `Ip != ""` as "answers", so any hop that replied once during the trace or MTR becomes a "later answering hop", and the router's ICMP rate limit on hop 2 is blamed on the provider. Routers that answer one TTL-exceeded and then rate-limit or deprioritise are common at the tail of paths into firewalled destinations.
What should happen: a 100% hop counts as an outage only when the hops after it (or the reached destination) also go to 100%. If nothing after it answers, or it is the last hop that answers, it is a quiet router again, and "judged by the last router that does" must name a hop that answered pings in the run.
Confidence: confirmed (LossStart/MtrVerdict on r2)

### N2. Outage log: "network changed" and "check switched off" events close an outage as if the internet came back
Severity: wrong result
Where: src\Monitor.cs lines 253 and 255 (new level-0 "inet" events), src\Brain\Outages.cs lines 40-42 (Pair: any Lv 0 closes)
How to reproduce:
- Internet quiet at 18:00 (3 failures).
- At 18:09 Wi-Fi reconnects with a new address (netChanged), or the user sets the internet check to Off.
- Pair input [quiet 18:00, level 0 at 18:09].
What happens:
- The log shows "Tue 10 Nov 18:00 - 18:09 · 9 min", a normal-looking outage with a made-up end. The event text itself says the end wasn't seen.
- The new `EndUnseen` wording ("end not seen … the network changed") is never used for this case. The fix closes the outage instead of marking its end unseen.
- If the network change happened during an outage that continues on the new network, the second "quiet" starts a fresh outage, so one outage is split in two with a fabricated gap.
What should happen: treat these two level-0 events as "end unseen" (EndUnseen = true, no length). Close an outage only on the "Internet back" event.
Confidence: confirmed (Pair on r2 with the event shape Monitor now emits)

### N3. ReplaceFile's fallback deletes the target before the move: points.txt, keys-ec.txt or settings can vanish
Severity: wrong result
Where: src\Program.cs line 211 (ReplaceFile, added in 3a888b4). It is now used by Points.Save, EcKeys.Save (the P-384 key index), AppCtx.SaveSettings, Brain stores, Gamer.SaveGame, History.Tidy and StreamOverlay.
How to reproduce: points.txt or keys-ec.txt is open in another process with FILE_SHARE_DELETE but not sharing for replace. Examples: an AV scanner, OneDrive or a backup agent, or an SMB share where File.Replace fails with IOException. Then trigger a save.
What happens:
- `File.Replace` throws IOException, then `File.Delete(dst)` succeeds (it becomes delete-pending), then `File.Move(tmp, dst)` fails (the name is still pending delete, or locked), and the exception propagates.
- The original is gone and the new data sits in `.tmp`.
- For keys-ec.txt that is exactly #20's outcome: every .adeebo addressed to you becomes "not made for any of your keys".
- For points.txt it wipes the total, the case #72 tried to close.
- Before 3a888b4 the failed Replace just left the old file in place.
What should happen: fall back only on PlatformNotSupported or "not supported" errors (not on every IOException). When falling back, move dst to `.bak` first and restore it if the move fails. Load should also fall back to `.tmp` or `.bak` when the main file is missing.
Confidence: suspected (read; Windows sharing behaviour)

### N4. Quit from the tray under a modal dialog still runs code on a disposed view, now with a false fix
Severity: crash
Where: src\AppCtx.cs line 292 (Quit, changed in 5a5ae0d for #164)
How to reproduce: open Ctrl+K (a Form) or Save/Export (a CommonDialog), then Exit from the tray.
What happens:
- For a modal Form, `f.Close()` only sets DialogResult. ShowDialog returns after this handler finishes, and by then `full.Close(); full.Dispose()` has already run, so `pal.Chosen()` and ShowTab run on the disposed view (ObjectDisposedException, logged as "UI error").
- CommonDialogs (SaveFileDialog) are not in Application.OpenForms, so they are never closed. Their owner window is destroyed under them.
- The behaviour is the same as round 1, but the comment now claims it is handled.
What should happen: in Quit, if any modal is open, close it and re-post Quit with BeginInvoke (so it runs after ShowDialog returns). Or check `IsDisposed` after every ShowDialog.
Confidence: suspected (read; WinForms modal-loop semantics)

### N5. The new SessionEnding handler touches WinForms objects off the UI thread
Severity: crash
Where: src\AppCtx.cs line 281 (OnSessionEnding, added in 5a5ae0d for #18) → src\Ui\FullView.cs line 734 StopCaptures (`jTimer.Enabled = false`, `jSess.Stop()`, `live.Stop()`)
How to reproduce: log off or shut down with the engineer view open on a packet journey.
What happens:
- SystemEvents can raise SessionEnding on its own thread. That is the reason 3a888b4 marshals UserPreferenceChanged (AppCtx.cs:137) in the same release, but this handler isn't marshalled.
- It sets a System.Windows.Forms.Timer from a foreign thread, which creates or destroys its hidden window on the wrong thread. It also stops captures concurrently with the UI's own Stop.
- The try/catch logs it, but a cross-thread timer change can also go silently wrong (the timer keeps firing), and CleanCaptures then races the still-running journey writer.
What should happen: `marshal.Invoke` (synchronous, so it finishes before logoff), then CleanCaptures.
Confidence: suspected (read)

### N6. SNMP Traffic on 32-bit counters: every real wrap is now shown as "reset"
Severity: wrong result
Where: src\Checks\SnmpTools.cs lines 171-180 (rate lambda, changed in 5a5ae0d for #97)
How to reproduce: Traffic on a device without ifXTable (no HC counters, which is exactly when `hc = false`) and a 1 Gb/s port at line rate. A 32-bit counter wraps every ~34 s, so about 15% of 5-second windows contain a wrap.
What happens: `hs` (ifHighSpeed, 1.3.6.1.2.1.31.1.1.1.15) comes from the same ifXTable that doesn't exist, so `port = 0` and the wrap branch returns -2 ("reset") for every genuine wrap. The busiest port can be shown as "reset" instead of near 100%. The fix traded the phantom 472 Mb/s for a phantom reset on the same devices.
What should happen: when ifHighSpeed is missing, take the port speed from ifSpeed (1.3.6.1.2.1.2.2.1.5, bits/s); if neither is known, accept a single wrap whose rate is ≤ 1 Gb/s or 10 Gb/s and label it "assumed one wrap".
Confidence: suspected (read; the logic is deterministic, no agent run)

### N7. A crafted IPv4-mapped address gets past the new "one device only" and speed-server guards
Severity: security
Where: src\Checks\Lab.cs line 68 (NotOne, added in 157e38d); src\Checks\SpeedTest.cs line 77 (SafeUrl → Watch.IsPublic)
How to reproduce:
- Traffic Lab udpsend to `::ffff:255.255.255.255:9` or ICMP builder to `::ffff:224.0.0.251`.
- A speedservers.txt line with `http://[::ffff:192.168.1.1]/cgi-bin/x`.
What happens:
- `NotOne` returns "" for all three mapped forms (it checks IPv6 only for IsIPv6Multicast), and `SafeUrl` returns True for `[::ffff:127.0.0.1]` and `[::ffff:192.168.1.1]` (confirmed on r2).
- On Windows a dual-mode socket sends these as IPv4 broadcast, multicast or LAN traffic. The new guards advertise "never to a whole network" and "can't aim at this PC, the router or a LAN address", but don't hold.
- SafeUrl also passes names that resolve to private addresses (`127.0.0.1.nip.io`, `localtest.me`, `router.asus.com`).
What should happen: unwrap with `IsIPv4MappedToIPv6 ? MapToIPv4()` before every check. For speed servers, check the resolved address at connect time.
Confidence: confirmed (NotOne/SafeUrl on r2); delivery on the wire suspected
