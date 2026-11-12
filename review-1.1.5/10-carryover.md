# 10 - Carry-over: 1.1.4 findings checked against dev-1.1.5

Baseline: `/home/user/AdeeboMeter` main @ 0965fa1 (1.1.4 + website commits). Target: `/home/user/AdeeboMeter-dev` @ f6d5747 (= origin/dev-1.1.5, clean tree; BRIEF.md confirms this is the code under review).
`git diff main origin/dev-1.1.5 --stat` shows 83 files changed. **docs/, README.md, CHANGELOG.md, SECURITY.md, CONTRIBUTING.md and .github/ISSUE_TEMPLATE are untouched.** The files below are also byte-identical between the two trees, so every finding in them carries over with the **same line numbers**: Snmp, SnmpGear, SnmpTools, RoutingTools, SysTools, Investigate, Investigate.Whats, WhyFails, HealthCheck, Inspection, WinEvidence, Icmp, DestCheck, Protocols, CertTools, SecChecks, Tools, MoreTools (all in src/Checks), Brain/Brain.cs, Capture/Packets.cs, Link/*, Apps/M365.cs, WindowsDeep/*, Knowledge/*. LiveCapture.cs changed only on the line it already had (a Policy check in `Start`), so its line numbers are also unchanged.
Search used: `git log -S/-G` over main..dev-1.1.5 found no commit aimed at any of these findings. The two partial changes (04-C8, 02-M4) came in as side effects of 1.1.5 features (commit f6d5747 "end-to-end PC test fixes", and 2501630 Store package).

Status key: STILL = still present; PARTIAL = partly fixed; FIXED; N/A = code removed. "Rule" refers to the BRIEF.md never-break rules (1 offline/About list, 2 never listens, 3 no admin, 4 secrets never kept, 5 SNMP read-only, 6 policy off-only, 7 low weight, 8 no Nokia, 9 examples never saved/counted, 10 points not farmable). "worse" means 1.1.5 widened the impact.

## 01-website.md (docs/ unchanged on dev-1.1.5)

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| H1 | High | privacy.html leaves out most traffic | STILL, worse | docs/privacy.html:43-49 (unchanged) | 1.1.5 adds more traffic that privacy.html, README and the site don't mention: the speed test (speed.cloudflare.com, Vultr, proof.ovh.*), Gamer Zone pings and api.steampowered.com, the TCP:443 fallback to 1.1.1.1, and the ISP report's cloudflare.com/cdn-cgi/trace. The in-app About now lists most of these (FullView.Settings.cs:369-385), so the gap between the app and the published policy is wider than it was. |
| M1 | Med | "online lookups only to RIPEstat/PeeringDB" is false | STILL | SecChecks.cs:365,381 (unchanged); About row FullView.Settings.cs:380 | The About "Online lookups (BGP)" row still names only stat.ripe.net and peeringdb.com. |
| M2 | Med | Header overflows <388 px | STILL | docs/index.html:50-57, 295-298 | |
| M3 | Med | Stale fallback versions, silent update check | STILL | docs/index.html:21,363,1003,1088-1108 | Note: dev still has `Program.Version = "1.1.4"` (Program.cs:66), so the `?v=` sent by the app is also stale until the version is bumped. |
| M4 | Med | PageUp/PageDown hijacked | STILL | docs/index.html:1038-1043 | |
| M5 | Med | ~880 px gap under slides on phones | STILL | docs/index.html:185-188, 302-303 | |
| M6 | Med | Dark-mode contrast failures | STILL | docs/index.html:33,38,55,57,83 | |
| M7 | Med | "103 tools" is wrong | STILL, worse | README.md:32, CHANGELOG.md:7, THEORY:235 | dev registers **129** unique tool ids in `Tools2()` (FullView.Toolbox.cs), up from 105. |
| L1 | Low | Stale version strings | STILL | same | |
| L2 | Low | Spoofable `?v=` banner text | STILL | docs/index.html:1099-1104 | |
| L3 | Low | privacy.html orphaned | STILL | docs/index.html, sitemap.xml | |
| L4 | Low | Stale sitemap, no robots.txt | STILL | docs/sitemap.xml | |
| L5 | Low | Slide footer numbering | STILL | docs/index.html | |
| L6 | Low | "One readable file" | STILL | docs/index.html:519, CONTRIBUTING.md:20 | |
| L7 | Low | Self-test claims ("30 checks", "<1 s") | STILL, worse | README.md:83 etc. | SelfTest.cs now registers 58 `T("…")` checks (was 35). |
| L8 | Low | "Loads nothing from elsewhere" but calls api.github.com | STILL | docs/index.html:24, 1088 | |
| L9 | Low | No captions; autoplay ignores reduced-motion | STILL | docs/index.html:419 | |
| L10 | Low | Keyboard/screen-reader polish | STILL | docs/index.html:185, 296 | |
| L11 | Low | privacy.html ignores chosen theme | STILL | docs/privacy.html:12-14 | |
| L12 | Low | README says "offline" | STILL | README.md:9,13 | Rule 1 wording |
| L13 | Low (susp) | Suggestion box params | STILL | docs/index.html:1077-1083 | |
| L14 | Low (susp) | Store section describes an unreleased listing | STILL | docs/privacy.html:54-55 | dev-1.1.5 now builds the Store package (store/Package.appxmanifest, build.yml "Store package" step). The page becomes accurate once the listing goes live. |

## 02-core.md

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| H1 | High | "Run from here" forces autostart, hijacks the installed copy's Run key, survives Uninstall | STILL | AppCtx.cs:40, 62, 208, 230; Setup.cs:410-413 | Only Store (packaged) copies are exempt (AppCtx.cs:226 `if (Program.Packaged) return`). The downloaded exe behaves exactly as before. |
| H2 | High | `GwEverReplied` never reset, giving a permanent "local network dragging" | STILL | Monitor.cs:220, 247 (netChanged still doesn't clear GwPing/GwEverReplied), 677, 686, 694 | The new TCP fallback helps only the Internet dot, not the LAN verdict. |
| H3 | High | False ARP-spoofing alarm across networks that share a gateway IP | STILL | Monitor.cs:415-449; Watch.cs:38 | |
| H4 | High | Wall-clock scheduling freezes probes and the sampler | STILL, worse | Monitor.cs:143-144, 212-257, 684; AppCtx.cs:269, 304, 310 | The new Gamer Zone loop repeats the pattern: Gamer.cs:385-416 (`lastRun`/`lastMinute` on UtcNow/Now). |
| M1 | Med | Router ping "Off" ignored while the view is open | STILL | Monitor.cs:212 | **Rule 1**: About says "Off." |
| M2 | Med | English-only netsh parsing; location block not detected | STILL | Monitor.cs:385-412 (k=="Signal" :397) | 1.1.5 added a 3-empty-reads hysteresis (Monitor.cs:405-406), which only covers busy/timed-out netsh. Localized and location-blocked output still ends with HasWifi=false. Side effect: a Wi-Fi adapter that really disappears is now noticed 3 intervals later. |
| M3 | Med | Alt+F4 during install leaves a half install | STILL | Setup.cs:275, 436; Program.cs:122-124 | |
| M4 | Med | Read-only exe folder: offer on every launch, settings never saved | PARTIAL | Setup.cs:54-55; Program.cs:60 (`WriteDir => Packaged ? DataDir : Dir`) | Fixed only for the Store/MSIX copy. A plain exe in Program Files or on a share still uses Program.Dir. A portable copy (`AdeeboMeter.portable`) in a read-only folder fails the same way. |
| M5 | Med | Downgrade labelled "Update", deletes the newer version | STILL | Setup.cs:280-281, 341 | |
| M6 | Med | Renamed copies escape StopOthers/KillOthers; "run here" copy not updatable | STILL | Setup.cs:94; Program.cs (Single) | |
| M7 | Med | "Slower than usual" right after switching networks | STILL | Brain/Brain.cs:193-212 (unchanged) | |
| M8 | Med | Compaction overwrites .gz; Lines ignores .gz when .csv exists | STILL, worse | History.cs:68 (File.Create .gz), 95 (Lines) | The new outage log (Brain/Outages.cs via `History.EventsOf`, History.cs:109) reads through the same `Lines()`, so a day hit by this loses its outages too. |
| M9 | Med | Offline/no-gateway get the wrong verdict | STILL | Monitor.cs:243 (ProbeDns runs with no gateway), 670-700 | |
| M10 | Med | Self-test writes fake events into real history | STILL | SelfTest.cs:780-783 to Model.cs:116 to History.Event | Rule 9 (spirit: made-up data saved as real) |
| M11 | Med | Reproducibility claim / dispatch builds the wrong ref | STILL | build.yml:28, 58-85; build_exe.cmd:21 | build.yml now also builds on pushes to `store`. |
| M12 | Med | Writers and readers lock different objects | STILL | Watch.cs:57/85-86, 205/223-224; readers Overview.cs:345, Investigate.cs:159, Insights.cs:68-75, SecChecks.cs:467; Cfg.Pins Program.cs:221 vs Watch.cs:245-248 | |
| L1 | Low | Upgrade re-enables autostart | STILL | Setup.cs:149, 227 | |
| L2 | Low | Local wall time in Ago/dedupe | STILL, worse | Model.cs:114, 126 | Outages.cs pairs events on local `T` (`Len = To - From`), so a DST fall-back during an outage gives a negative length in the outage log. |
| L3 | Low | Negative time-of-day row crashes Week() | STILL | History.cs:131 (no t<0 check), 159 | |
| L4 | Low | Log: no rate limit, wipe at 256 KB, culture time, raw newlines | STILL | Program.cs:148-158; AppCtx.cs:344 | Rule 7 |
| L5 | Low | Header and self-test privacy claims | STILL | Program.cs:5-8; SelfTest.cs | The header also leaves out the TCP:443 fallback and the Gamer Zone. |
| L6 | Low | Uninstall leaves %TEMP%\AdeeboMeter-remove.exe | STILL | Setup.cs:196 | |
| L7 | Low | CRLF in the inner SHA256SUMS | STILL | release.ps1:54 | |
| L8 | Low | Crash URL too long | STILL | Watch.cs:274-277 | |
| L9 | Low | OpenUrl prefix allowlist is loose | STILL | Watch.cs:280-285 (`Allowed`) | Refactored into `Crash.Allowed` with PayPal added. The GitHub and Pages entries are still bare `StartsWith` prefixes. |
| L10 | Low | Adapter choice ignores route metric | STILL | Monitor.cs:282-300 | |
| L11 | Low | File.Replace fails on SMB/USB | STILL | AppCtx.cs:216; Brain.cs:110 | |
| L12 | Low | Misc (write-icon crash, case-sensitive flags, Take(20000), Conns by path, Brain.Bg thread/5 s, "router still answers") | STILL | Program.cs:118; Brain.cs:76-81; Watch.cs:39, 133 | New `--render-card` (Program.cs:111) and `--seconds` also match case-sensitively. Brain.Bg is Rule 7. |
| S1 | Med (susp) | Defender/SmartScreen heuristics | STILL | Setup.cs | The Store copy avoids it (MS-signed). |
| S2 | Med (susp) | Localized netsh labels | STILL | Monitor.cs:397 | |
| S3 | Low (susp) | PerformanceCounter blocking at start | STILL | Monitor.cs | |
| S4 | Low (susp) | Extra forward lookup / hung resolver | STILL | Monitor.cs | 1.1.5 adds more `Dns.GetHostEntry(IPAddress)` calls, up to 64 in parallel (Reach.cs:284, Find devices). |
| S5 | Low (susp) | MAX_PATH | STILL | | |
| S6 | Low (susp) | SystemEvents handlers off the UI thread | STILL | AppCtx ctor | |

## 03-tools-input.md

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| C1 | High | iperf3 `-u -l <12` kills the app | STILL | Toolkit.cs:57 (no lower bound), 227, 240 | |
| C2 | Med | Traffic Lab UDP 10 pkt/s cap bypassed against a closed port | STILL | Lab.cs:160-169 | Breaks the BRIEF guard "capped at 10 packets a second". |
| C3 | Med | Broadcast/multicast/0.0.0.0 accepted as "one target" | STILL | Lab.cs:42-48 (Split), 81 (ICMP regex), 157-162 | The new ICMP `dscp`/`compare` path (Lab.cs:137-140, raw `Icmp.Echo`) also accepts these targets. |
| C4 | Med | PowerShell injection via smart quotes in cert commands | STILL | CertTools.cs:301, 313-319 (unchanged) | |
| C5 | Med | Security checkup bypasses the company-network gate | STILL | SecChecks.cs:408, 432, 443 | `Policy.NoLab` is now checked inside `Lab.Allowed` (Lab.cs:36), so the policy is honoured (Rule 6 OK). The company-network tick is still forced to "yes". |
| C6 | Med | LocalMachine interception roots never flagged | STILL | SecChecks.cs:216-223 | |
| C7 | Med | Saved Wi-Fi green on non-English Windows | STILL | SecChecks.cs:328-337 | |
| C8 | Med | HTTP builder slow-loris hang, ignores Stop | STILL | Reach.cs:32, 65 | |
| C9 | Med | Mail tool unbounded reads | STILL | Protocols.cs:184-199 | |
| C10 | Med | URL input breaks ping/trace tools (Icmp.Resolve4) | STILL | Icmp.cs:100-101 | Same root cause as 04-C5. |
| C11 | Low-Med | TLS 1.3 "not offered" on Win10 | STILL | Toolkit.cs:645-656 | Now also reached through the new SNI parameter. |
| C12 | Low-Med | dig silently falls back to the system DNS | STILL | Toolkit.cs:369 | |
| C13 | Low-Med | IDN sent as '?' | STILL | Toolkit.cs:442; Reach.cs:125; MoreTools.cs:125 | |
| C14 | Low-Med | Hash compare false mismatch | STILL | CertTools.cs:128 | |
| C15 | Low | Subnet calc bad prefix becomes /32 | STILL | Toolkit.cs:789-795 | |
| C16 | Low | Scoped IPv6 + port loses the port | STILL | DestCheck.cs:70-73 | |
| C17 | Low | Misc parsing slips | STILL | Toolkit.cs:553 (`StartsWith("http")`), 418-424 (Query TCP fallback); Protocols.cs:31, 66; CertTools.cs:283; MoreTools.cs:164, 207, 263; Reach.cs:34; Lab.cs:248 (stride, no DhcpCApiCleanup) | |
| C18 | Low | DeepTest contacts the company DC | STILL | FullView.DeepTest.cs:24 | ByHand grew a lot, but `ldapcheck` and `shares` are still missing from it. |
| S1 | Med (susp) | Inspect a certificate may persist a private key | STILL | CertTools.cs:88 | **Rule 4** if confirmed |
| S2 | Low-Med (susp) | ICMP builder to broadcast/multicast | STILL | Lab.cs:81 | |
| S3 | Low (susp) | IPv4-only TcpClient in Mail/Revocation/Tls | STILL | Toolkit.cs:650 etc. | |
| S4 | Low (susp) | BitLocker value mapping | STILL | SecChecks.cs:106-108 | |
| S5 | Low (susp) | Company guard heuristic | STILL | Lab.cs Company | |
| S6 | Low (susp) | Update date culture | STILL | SecChecks.cs:131 | |

## 04-snmp-investigate.md

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| C1 | High | Find a MAC is wrong past 2,000 FDB entries | STILL | SnmpTools.cs:49; Snmp.cs:348-365 (unchanged) | |
| C2 | Med | Walk accepts non-increasing OIDs | STILL | Snmp.cs:355-361 | |
| C3 | High | "Router doesn't answer at all" when the gateway drops ICMP/TCP | STILL | Investigate.Whats.cs:69-80; Investigate.cs:86-88; FullView.Replay.cs:170; FullView.Story.cs:74 | |
| C4 | High | Captive portal blamed on the ISP | STILL | Investigate.Whats.cs:84-94 | |
| C5 | High | VPN flow says green "door opens" for a URL target | STILL | Icmp.cs:100; Investigate.cs:255-268 | |
| C6 | Med | Amber observations, green conclusion | STILL | Investigate.cs:94; Investigate.Whats.cs:86 | |
| C7 | Med | 32-bit counter reset shows as a phantom rate | STILL | SnmpTools.cs:168-172 | |
| C8 | Med | MTR blames "real loss from hop N" for destination-only loss | PARTIAL | PathTools.cs:101-131 (`LossStart`/`MtrVerdict`) | Fixed: silent hops are skipped, and a lossy last-answering hop before silence is no longer blamed. Still present: with a reached destination at ≥3 % and clean earlier hops, `later` is empty, `h.Reached` is true, and `All()` is vacuously true. That still returns "Real loss starts at hop N … carries on to the end", and the "Loss at the destination only / rate-limits pings" text remains unreachable at ≥3 %. This is now deliberate (SelfTest.cs:417 asserts it), and it feeds the **ISP report** (IspReport.cs:86-87, 128), which blames the line for a server that only rate-limits ICMP. Worse in reach. |
| C9 | Med | IPv6 vs IPv4: "Neither connected." in green | STILL | PathTools.cs:211-214 | |
| C10 | Med | Any DHCP client counted as a second server | STILL | RoutingTools.cs:160-161, 226 | |
| C11 | Med | Gateway takeover logged on network moves | STILL | RoutingTools.cs:35-46 | |
| C12 | Med | HealthCheck Wi-Fi "calm" on non-English Windows | STILL | HealthCheck.cs:76 | |
| C13 | Med | Paint loops re-spawn work when the loader fails | STILL | FullView.Travel.cs:52-59, 133, 186; FullView.Insights.cs:18-21 | Rule 7 (busy loop) |
| C14 | Low-Med | n/a shown as 0 (Num/ReadInt) | STILL | Snmp.cs:158, 374, 395; SnmpGear.cs:88, 140, 270; SnmpTools.cs:242-243 | |
| C15 | Low-Med | Replay stuck "Reading…"; corrupt row breaks paint | STILL | FullView.Replay.cs:50, 126; History.cs:159 | |
| C16 | Low | Full-tunnel VPN flagged as overlapping the LAN | STILL | PathTools.cs:187-193 | |
| C17 | Low | "No route" shown green | STILL | RoutingTools.cs:123 | |
| C18 | Low | w32tm error shown green | STILL | SysTools.cs:298 | |
| C19 | Low | Snapshot limit text; slow Stop on v3 | STILL | SnmpGear.cs:21; Snmp.cs:351 | |
| C20 | Low | TeamsUdp NAT verdict from one server | STILL | WhyFails.cs:144, 167-170 | |
| C21 | Low | SNMP Interfaces reboot/renumber detection | STILL | SnmpTools.cs:99, 130, 136 | |
| S1 | Med (susp) | DNS step on DoH / dead primary | STILL | Investigate.Whats.cs:98-109 | |
| S2 | Med (susp) | IPv6-only and multi-adapter context | STILL | Monitor.cs:282-300 | |
| S3 | Med (susp) | Ring averages hide a dead link | STILL | Model.cs Ring.Avg | |
| S4 | Med (susp) | MtuAt treats ICMP rate limiting as a black hole | STILL | PathTools.cs:44-62 | |
| S5 | Low-Med (susp) | WinEvidence success IDs counted as failures | STILL | WinEvidence.cs:93 | |
| S6 | Low (susp) | Localized command output | STILL | WhyFails.cs:55-57; SysTools.cs:165-174 | |
| S7 | Low (susp) | "Public CA" by name substring | STILL | Inspection.cs:60-64 | |
| S8 | Low (susp) | Community trimming/ASCII, v3: prefix, forged first packet | STILL | Snmp.cs:33-35, 114 | |
| S9 | Low (susp) | Linux RAM includes cache | STILL | SnmpTools.cs:206 | |
| S10 | Low (susp) | IPv6-only listeners missed | STILL | SysTools.cs:240 | |

## 05-ui.md

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| C1 | High | Ctrl+Alt+A steals AltGr+A | STILL | Controls.cs:405 | The self-test still checks only that the key is free (SelfTest.cs:789). |
| C2 | Med | Widget over full-screen and shared screens | STILL | AppCtx.cs:297-344; Controls.cs:57 | No SHQueryUserNotificationState or SetWindowDisplayAffinity in the OnUi path. |
| C3 | Med | Section tabs / bars clip at small sizes | STILL, worse | FullView.cs:141, 216, 240; Inside.cs:27 | Pro mode now has 9 sections (Gamer Zone added, FullView.cs:410). The header gains a Points chip and a wider SlideSwitch, and the French captions are longer. Simple mode (4 sections) hides the problem. |
| C4 | Med | Connections list ignores small wheel deltas | STILL | Controls.cs:393 | |
| C5 | Med | Settings paint mutates live Cfg (race) | STILL | FullView.Settings.cs:221, 248-254 | **Rule 1**: an "Off" probe can fire |
| C6 | Med | Import wipes pins before validating; widget/theme counted but ignored | STILL | FullView.Settings.cs:516-522; Program.cs (Read has no widget/theme case) | |
| C7 | Med | One-click "Delete all saved history" | STILL | FullView.Settings.cs:232-237 | |
| C8 | Med | Esc in a text box closes the view | STILL, worse | FullView.cs:309-313 | The new Settings search box (FullView.cs:532) also closes the view on Esc. |
| C9 | Low-Med | Fixed 264x300 cover zone | STILL | AppCtx.cs:293 | |
| C10 | Low-Med | Font.GetHashCode measure-cache collisions | STILL | Theme.cs:212 | |
| C11 | Low | "Listening in for 30 s" hard-coded | STILL | FullView.cs:891 | |
| C12 | Low | Save/Clipboard failures silent | STILL | FullView.Tools.cs:47; Paint.cs; new Guide "Copy for someone" (Guide.cs:148) also only logs | |
| C13 | Low | Detail card / tip not clamped; unclipped DrawString | STILL | Theme.cs:496+; Controls.cs:503 | |
| C14 | Low | Trunc cuts surrogate pairs | STILL | Theme.cs:226 | |
| C15 | Low | Tray tooltip rewritten every render | STILL | AppCtx.cs:361 | Rule 7 (minor) |
| C16 | Low | Disk/process I/O inside Paint | STILL | FullView.Settings.cs:132, 232; Inside.cs:203 | Rule 7 |
| C17 | Low | Changes chart cache key ignores reload | STILL | FullView.Paint.cs:291-295 | |
| S1 | Med (susp) | System-aware DPI only | STILL | Program.cs:83 | |
| S2 | Med (susp) | AutoScroll oscillation | STILL | Need()/Paint* | Rule 7 |
| S3 | Med (susp) | Widget over the tray menu | STILL | Controls.cs:57 | |
| S4 | Med (susp) | Second launch opens behind other windows | STILL | AppCtx.cs:253 | |
| S5 | Low-Med (susp) | Watchdog catch too narrow | STILL | FullView.cs:76 | |
| S6 | Low (susp) | Quit under a modal dialog | STILL | AppCtx Quit | |
| S7 | Low (susp) | Segoe UI Variable / fallback glyphs | STILL | Theme.cs:189-190 | |
| S8 | Low (susp) | PrimaryScreen null | STILL | AppCtx.cs:289 | |
| S9 | Low (susp) | Bidi/zero-width in SSIDs | STILL | | |
| S10 | Low (susp) | Key auto-repeat toggles buttons | STILL, worse | Controls.cs:125 | The new SlideSwitch (Controls.cs:427) and ThemeSwitch (:455) have the same pattern. |
| A1 | High | Painted UI invisible to AT and keyboard | STILL | everywhere | The new Points, Gamer, ToolHome and Examples pages are painted too. |
| A2 | Med | RoundBox has no accessible name | STILL | Controls.cs:181-190 | Only the new Settings search sets it by hand (FullView.cs:533). |
| A3 | Med | PillButton name and state go stale | STILL | Controls.cs:109, 115, 126 | The `Text` setter now translates, but AccessibleName is still set once in the ctor. |
| A4 | Med | Tab order reversed | STILL | FullView.cs:545-546 | |
| A5 | Med | High contrast ignored | STILL | Theme.Apply | |
| A6 | Low | ThemeSwitch has no role or state | STILL | Controls.cs:447-455 | SlideSwitch got Role=CheckButton but still exposes no checked state. |
| A7 | Low | ConnList keyboard; text scaling | STILL | Controls.cs:211+ | |
| A8 | Low | Widget is hover-only | STILL | | |

## 06-capture-link-deep.md

| ID | Sev | Title | Status | New location | Note |
|---|---|---|---|---|---|
| H-1 | High | Ring and sniff captures survive a crash or kill; CleanCaptures isn't recursive | STILL | Program.cs:185-187; AppCtx.cs:66, 283 | Private traffic on disk (privacy promise) |
| M-1 | Med | Sniffs have no size cap | STILL | FullView.cs:886; LiveCapture.cs:366 | |
| M-2 | Med | Wi-Fi page English-only, says "great shape" on no data | STILL | Link/LinkInfo.cs:345-551; Monitor.cs:397 | |
| M-3 | Med | Docs claim the advisor and twins work without location | STILL | Link/WifiAdvisor.cs:77-81; THEORY:226 | |
| M-4 | Med | Evil-twin OUI false alarms | STILL | Link/WifiAdvisor.cs:113-121 | |
| M-5 | Med | Live capture says "capturing" after tshark dies | STILL | LiveCapture.cs:146-153 | |
| M-6 | Med | WMI disk query has no timeout | STILL | WindowsDeep/PcHealth.cs Disk() | |
| L-1 | Low | Second-resolution sniff folders collide | STILL | LiveCapture.cs:360 | |
| L-2 | Low | Non-ring sniff is promiscuous | STILL | FullView.cs:886 (no `-p`) | |
| L-3 | Low | "valid" substring disables extras | STILL | LiveCapture.cs:107-108 | |
| L-4 | Low | BandOf / FromFreq 6 GHz | STILL | Link/LinkInfo.cs:346-350; :191 | |
| L-5 | Low | Startup list hidden by an illegal path char | STILL | WindowsDeep/Inside.cs:146, 170-175 | |
| L-6 | Low | DHCPCAPI stride 32 on x86 | STILL | Lab.cs:248 | The build is now explicitly `/platform:anycpu` (build_exe.cmd, release.ps1), so it still runs 32-bit on x86 Windows. |
| L-7 | Low | ProcTree PID reuse | STILL | WindowsDeep/Inside.cs:24, 39-44 | |
| L-8 | Low | M365.Role substring match | STILL | Apps/M365.cs:31-32 | |
| L-9 | Low | "on battery 255%" | STILL | WindowsDeep/PcHealth.cs Pressure() | |
| L-10 | Low | Parser exceptions (int.Parse, Arp.Randomised, CloseText) | STILL | Link/LinkInfo.cs:253, 373; Knowledge/Explain.cs:115 | |
| L-11 | Low | Misc (30 s text, dead null check, fe80 scope, battery temp file, multi-adapter, netsh encoding) | STILL | FullView.cs:891; Packets.cs:38 | |
| S-1 | Low (susp) | NBSP before % | STILL | Link/LinkInfo.cs:368, 416 | |
| S-2 | Low (susp) | 24H2 location gates show interfaces | STILL | Monitor.cs:385-412 | |
| S-3 | Low (susp) | tshark child before Job.Add | STILL | | |
| S-4 | Low (susp) | Stop2 kills only tshark | STILL | | |
| S-5 | Low (susp) | GPU Engine counters | STILL | | |

## Regressions and widened findings in 1.1.5

1. **MTR/ISP report (04-C8):** loss only at a reached destination is now reported by design as "Real loss starts at hop N … carries on to the end" (PathTools.cs:101-131, asserted by SelfTest.cs:417). The new ISP report, which users paste to their provider, carries this conclusion (IspReport.cs:86-87, 128). The BRIEF's "loss counts only when it carries on to later hops" rule is broken for the last hop.
2. **Wall-clock scheduling (02-H4) copied into the Gamer Zone loop** (Gamer.cs:385-416). A backwards clock change freezes background game pings and minute history the same way.
3. **The outage log inherits history bugs.** It reads `History.Lines`, which skips `.gz` when a `.csv` exists (02-M8). It also pairs local wall-clock timestamps, so DST gives negative lengths (02-L2), via Outages.cs and History.EventsOf.
4. **Esc closes the view from the new Settings search box** (05-C8).
5. **Tab-strip and header overflow (05-C3):** worse with the 9th section (Gamer Zone), the Points chip, the SlideSwitch and French captions.
6. **The SlideSwitch repeats 05-S10/A6.** Key auto-repeat toggles Details on and off, and it exposes no checked state.
7. **ProbeWifi hysteresis (Monitor.cs:405-406)** delays detection of a real loss of the Wi-Fi interface by 3 Wi-Fi intervals. The localized and location-blocked cases (02-M2) are unchanged.
8. **Privacy docs drift further (01-H1/M1, 02-L5).** The speed test, Gamer Zone (incl. api.steampowered.com), the TCP:443 fallback and the ISP report's cloudflare.com trace are not in privacy.html, the README or the Program.cs header. The in-app About covers most of them.
9. **Docs numbers drift further:** 129 tools (README says 103), 58 self-test checks (docs say 30).
10. **Cosmetic, French:** FullView.Paint.cs:87 measures the English "Windows counters · no admin" but draws the translated string, so the right-aligned label is mispositioned when translated. The same measure-English/draw-translated pattern appears for "AdeeboMeter" in Theme.cs (GamerCard/MetricCard `nw`).
11. **Version not bumped on dev:** `Program.Version` is still "1.1.4" (Program.cs:66). Crash reports, the User-Agent and the update check identify as 1.1.4.
12. **03-C3 extended:** the new ICMP `dscp … compare` mode (Lab.cs:137-140) sends raw echoes to the same unchecked broadcast or multicast targets.

## Counts

| Source | Findings | STILL | PARTIAL | FIXED | N/A |
|---|---|---|---|---|---|
| 01-website | 22 | 22 | 0 | 0 | 0 |
| 02-core | 34 | 33 | 1 (M4) | 0 | 0 |
| 03-tools-input | 24 | 24 | 0 | 0 | 0 |
| 04-snmp-investigate | 31 | 30 | 1 (C8) | 0 | 0 |
| 05-ui | 35 | 35 | 0 | 0 | 0 |
| 06-capture-link-deep | 23 | 23 | 0 | 0 | 0 |
| **Total** | **169** | **167** | **2** | **0** | **0** |

Of the STILL items, 13 are marked "worse": 01-H1, M7, L7; 02-H4, M8, L2; 05-C3, C8, S10; plus the widened 04-C8 (PARTIAL), 03-C3, 02-L5 and 02-M2 noted in the regressions above.
