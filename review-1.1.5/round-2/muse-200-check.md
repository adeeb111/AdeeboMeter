# Check of Muse's 200-bug list: Mediums, Medium-Lows and a Low sample

Code: `/home/user/AdeeboMeter-dev` at `a0fbd56`, read-only. Harness: `scratchpad/review2/harnessM/m.cs`, run with mono against `build2/r2.dll` (reflection). Where mono and .NET Framework differ, I went by .NET Framework behaviour.

What the verdicts mean:
- **REAL**: the defect exists, at about the severity Muse gave.
- **OVERSTATED**: the defect exists, but at least one band lower. Text, docs and EXAMPLE-banner content all count as Low.
- **WRONG**: not a defect, or the stated mechanism is false.
- **DUPLICATE**: already in round 1 (`claude-code.md #n`) or in a round-2 `R2-*.md` file.

Muse numbers 51 Medium defects across 48 entries: #47 counts as 2 and #54 as 3. There are 37 Medium-Lows. The Low sample is 20 of the 102 Low bullets, drawn with `random.seed(20261112)`.

## Counts

| Group | REAL | OVERSTATED | WRONG | DUPLICATE | Total |
|---|---|---|---|---|---|
| Medium | 20 | 18 | 8 | 5 | 51 |
| Medium-Low | 1 | 26 | 8 | 2 | 37 |
| Low sample | 16 | 0 | 4 | 0 | 20 |

How to read this: about 84% of the Mediums are real defects (REAL + OVERSTATED + DUPLICATE). Only 20 of 51 hold up at Medium. About 78% of the Medium-Lows are real, but nearly all of those are Low: example text, docs counts, Duration strings and French wording. In the Low sample, 16 of 20 are real (about 80%).

## Medium (51)

| # | Muse item (short) | Verdict | Where | Evidence |
|---|---|---|---|---|
| 8 | SNMP non-ASCII text shown as hex / fake MAC | REAL | Checks\Snmp.cs:415-416 | ran: "café" → `0x636166c3a9`, 6-byte "Bürox" → `42:c3:bc:72:6f:78`. The text test rejects continuation bytes 0x80-0xBF. |
| 9 | FindMac ARP suffix matches the wrong device | WRONG | Checks\SnmpGear.cs:76 | ran: the ipNetToMedia index is always `ifIndex.a.b.c.d` (RFC 1213). `"10.192.168.1.5"` is ifIndex 10 + 192.168.1.5, which is the right entry. A 4-octet suffix after a dot can only match the exact IP. |
| 10 | Bandwidth "1,000 MB" read as 1 MB | DUPLICATE | Checks\Calc.cs:170 | R2-calc-labs #4. ran: 1,000 MB @ 100 Mbps → "80 ms". |
| 11 | Number converter reads 100000000 as binary | DUPLICATE | Checks\Calc.cs:226 | R2-calc-labs #5. ran: "Read as binary · 256". |
| 12 | Modem check mixes upstream power; ignores uncorrectables | REAL | Checks\IspReport.cs:247-251 | The power regex takes every "Power … dBmV" (upstream is about +35..+55, which fits `\d{1,2}`) and judges against -7..+7. `unc` is shown but never used in the level. |
| 13 | HTTP check echoes user:password from the URL | OVERSTATED | Checks\Reach.cs:79 | `Uri.ToString()` keeps userinfo, so it's true. But it is the user's own typed input echoed back into their own result. Low. |
| 14 | Net-SNMP import: Counter32/Gauge32 "(too long)" | REAL | Checks\SnmpTools.cs:334 vs Snmp.cs:425 | ran: `FromNetSnmp` on a numeric walk gives `InOctets (too long)`, `Speed (too long)`, `Mtu 1500`, `ifHCInOctets 99`. |
| 15 | MTR Duration clamps at 10 min, tool allows 60 | OVERSTATED | Ui\FullView.Toolbox.cs:461 vs :32 | True (`Math.Min(10, n)` against `Math.Min(60, min)`), but it is only an estimate string. Low. |
| 16 | games-*.csv "router ms" column always empty | OVERSTATED | Gamer\Gamer.cs:507,514 | `gw` is never assigned. Nothing reads column 8 (`Read` stops at c[6]), so nothing a user sees is wrong. Low. |
| 17 | RTP sequence restart counted as a wrap, ~99% loss | OVERSTATED | Capture\LiveCapture.cs:260 | ran: same SSRC, 40000..40999 then 1000..1999 → "92.7% · MOS 1.0". Real, but senders that restart normally pick a new SSRC (a new stream key). It needs a same-SSRC sequence jump, e.g. some SBC re-anchors. |
| 18 | New-device pop-ups stop once 30 MACs have ever been seen | REAL | Brain\Watch.cs:244 | `set` is the cumulative known-MAC set, randomised MACs included. It is never pruned and is tested with `set.Count > 30`. |
| 19 | Wi-Fi advice matches APs by SSID only | OVERSTATED | Link\WifiAdvisor.cs:103,109 | An SSID is the ESS identity in 802.11, and Windows roams by SSID too, so "same network = same SSID" is the right model. Only a same-SSID neighbour or a twin is mis-handled. Low. |
| 20 | Neighbour wars counts same-SSID APs as mine | DUPLICATE | Link\AirLog.cs:29 | R2-hof-points-update #10 ("key mine by BSSID rather than by SSID"). |
| 21 | Journey rewind packets never re-filtered to the found remote | WRONG | Capture\Journey.cs:432 | `Rewind()` returns an empty list when `F.Remote == ""`, so there is never an unfiltered "before" set to mix in. |
| 22 | "Evenings with trouble" threshold uses NaN→0 average | OVERSTATED | Gamer\Gamer.cs:561 | True. But all-NaN minutes have `Loss` 100, which flags that evening anyway. The effect is a mildly lower threshold. Low. |
| 23 | Video streams never get a clock rate | REAL | Capture\LiveCapture.cs:264 | ran: 200 pps at 90 kHz → kind "media", jitter "-". The code has a "video" label (`Rate >= 90000`) that this path can never reach. |
| 24 | "Capped by the receive window" ignores window scaling | REAL | Capture\Journey.cs:330-332 | The scaled raw window is small (e.g. 501), so `mbps > cap*0.7` fires on almost every fast scaled flow. The `< 65535` guard doesn't help, and the SYN usually isn't in the capture. |
| 25 | Baseline counts silent minutes → "Normally 0 ms" | REAL | Brain\Brain.cs:237-240,253 | ran: 120 minutes with gw NaN → "Normally 0 ms to your router and 20 ms to the internet". The later alert compares against `b.Gw = 0`. |
| 26 | Channel overlap ignores width | DUPLICATE | Link\LinkInfo.cs:458 | R2-hof-points-update #9 (same `Overlaps`, ran there). |
| 27 | OldTls: unreachable site shown green "refused" | REAL | Checks\SecChecks.cs:524-526 | Null from both `SiteCert` calls gives level 0 and `e1`/`e2` are never read. `CertExpiry` gives 3 on null. |
| 28 | Machine-roots fix link (`certlm.msc`) does nothing | REAL | Ui\FullView.Security.cs:60-68; SecChecks.cs:228 | No branch matches. `SecTools` has `certmgr.msc` but not `certlm.msc`, so the click is silent. |
| 29 | RouterOld green with only 8080 / 22 open | WRONG | Checks\SecChecks.cs:447-455 | Weight 3 in `OldPorts` means "list it, not a problem" (level 3 = info in this app). Only weights 1-2 count as `bad`. SSH or 8080 on the LAN side isn't an old-protocol risk, so this is by design. |
| 30 | SmbSign says "not required" on 24H2 | WRONG (likely) | Checks\SecChecks.cs:173 | On 24H2 the default is delivered as `RequireSecuritySignature = 1` in `LanmanWorkstation\Parameters`; secpol shows "Enabled", and the NAS guides say to change it from 1 to 0. So the code reads "required". The row is also grey (level 3), not a warning. I did not check this on a live 24H2 machine. |
| 31 | Startup misses WOW6432Node Run keys | REAL | Checks\SecChecks.cs:266 | Since Win7, `HKLM\…\CurrentVersion\Run` is redirected for 32-bit writers, so `WOW6432Node\…\Run` is never read. Muse's CU half is moot because HKCU\Software is shared, not redirected. |
| 32 | Startup skips non-.exe entries | OVERSTATED | Checks\SecChecks.cs:274 | True, but the row says "all **.exe** programs signed", so the scope is stated. Low. |
| 33 | PFX chain ignores `Build()` result | OVERSTATED | Checks\CertTools.cs:193-196 | True: CAPI returns the best chain with status flags (e.g. NotSignatureValid) and the code reads only the count and self-signed end. The trigger is a mis-signed chain inside a PFX, which is exotic. Low. |
| 34 | PFX "Key present and usable" doesn't check it matches | OVERSTATED | Checks\CertTools.cs:184-187 | True against the header's promise, but `openssl pkcs12 -export` refuses a mismatched pair ("No certificate matches private key"), so it needs a hand-built file. Low. |
| 35 | WifiSec English-only regex | OVERSTATED | Checks\SecChecks.cs:351 | True ("Authentifizierung"/"Authentification" don't match). The result is a grey "couldn't tell", not a wrong verdict. Same class as round-1 #65-68. |
| 36 | Match() leaves the old self-signed entry | OVERSTATED | Checks\CertKeys.cs:234-235 | True: `Persist` imports a PFX, so you get a second entry with its own key copy. "Now one entry" describes the new entry. Cosmetic in My keys. Low. |
| 37 | Delete() leaves the private key | REAL | Checks\CertKeys.cs:351-359 | `X509Store.Remove` never deletes the key container. Muse's "no managed API" is wrong, though: `CngKey.Delete()` exists (EcKeys.cs:86 uses it), and CAPI keys go with `PersistKeyInCsp = false` + `Clear()`. |
| 38 | KillOthers kills the uninstall helper | DUPLICATE | Program.cs:451-466 | R2-core-tools #10. |
| 39 | SPF `+all` reported "SPF set" green | REAL | Checks\SecChecks.cs:568-569 | `+all` lets anyone pass SPF, aligned, so DMARC passes too. Level 0 is wrong. |
| 40 | Watch: versioned-folder apps flagged "no publisher" | REAL | Brain\Watch.cs:154-160 | `path = key` (with `*`) happens before `FileFacts(path)`. `GetVersionInfo` throws, the catch-all gives empty facts, and the row goes red level 2. |
| 41 | MTU per hop: first measured hop always "drops here" | REAL | Ui\FullView.Toolbox.cs:1036,1044 | `prev = 99999` and `drop = h.Mtu < prev`, so a 1500 first hop paints amber "← drops here". (read; deterministic) |
| 42 | Overview Describe-it box detaches when scrolled | WRONG | Ui\FullView.Overview.cs:302 | In WinForms a child's `Location` is in client (display) coordinates. Scrolling moves children, so client = virtual + `AutoScrollPosition`. That is exactly the code. The click mappings go the other way (client→virtual), which is why they subtract. |
| 43 | "printer" in Describe-it → no-internet guide | OVERSTATED | Ui\FullView.Overview.cs:137 | A deliberate mapping, added in the round-1 #77 fix (fixcheck: PARTIAL). The guide is "check every layer"; only the "Sounds like there's no internet" hint wording is off. Low. |
| 44 | Replay blames CPU for a memory-triggered minute | OVERSTATED | Ui\FullView.Replay.cs:169,181 | True (mem > 92 with cpu 20 → "CPU 20%"). One phrase, and memory % is shown on the strip above. Low. |
| 45 | Link page "no neighbouring networks" text is dead | OVERSTATED | Ui\FullView.Link.cs:91-93 | True dead `else if`. The only effect is a missing hint. Low. |
| 46 | About: "7-day history is off unless you switch it on" | REAL | Ui\FullView.Settings.cs:320 vs Program.cs:301,303 | History defaults on (`History = true`) with 3 days. A false privacy statement in-app. |
| 47a | DestCheck Copy: card rows lose their name | REAL | Checks\DestCheck.cs:47 | `Skip(1).Take(2)` on 3-element rows prints "value: level" (e.g. PATH MTU "1400 bytes: 1"). |
| 47b | DestCheck Copy: all Findings dropped | REAL | Checks\DestCheck.cs:46 | Findings are 2-element `[text, level]` (:544-577) and the guard is `r.Length > 2`. |
| 48 | TWAMP Stop: "sent" is the requested count | REAL | Checks\Protocols.cs:451,480 | `sent = n`, while the loop exits on `run.Stop`, so the denominator and the "unknown" split are both wrong after Stop. |
| 49 | Routing listen ignores Stop; quiet network captures twice | REAL | Checks\RoutingTools.cs:144-146 | No `run.Stop` read. An empty first result re-runs the full `-a duration` capture. |
| 50 | VPN guide "looks fine" with a level-1 overlap shown | REAL | Checks\Investigate.cs:251-275 | `bad` takes level 1 or 2, but only `bad[0]=="2"` sets a verdict. `Conclude(0, …)` doesn't look at observations. Same class as round-1 #64, in a flow that fix didn't touch. |
| 51 | STUN to IPv6 server: "answered, but not with an address" | OVERSTATED | Checks\Reach.cs:122 | True (family 1 only). `Lab.Addr` prefers IPv4, so it needs an IPv6 literal or a v6-only host. Low. |
| 52 | IPv6 ping reads the reply address at the wrong offset | WRONG | Checks\Icmp.cs:89 | `IPV6_ADDRESS_EX` is declared under `pshpack1` (26 bytes: port 2, flowinfo 4, addr 16, scope 4), so the address is at **6**. The reference source's `Ipv6Address` is `Pack=1 { byte Goo[6]; byte Address[16]; uint ScopeID }`. Status is at 28 and RTT at 32, as the code reads. |
| 53 | Key-backup restore decrypts into unbounded memory | OVERSTATED | Checks\EcKeys.cs:181-183; FileCrypt.cs:129-139 | True: one pass decrypts into a MemoryStream before the tag is checked, so any passphrase plus a multi-GB file grows memory. It needs the user to pick a huge file as a "key backup". Low. |
| 54a | radius example: empty secret refused | OVERSTATED | knowledge\examples.txt `== radius` | The empty box is by design: secret boxes are masked and "Fill the boxes" skips them (Toolbox.cs:264, :212 `SecretInput`). The row text is still wrong: it says "Access-Reject … it's alive", but the tool's Status-Server marks Access-Reject amber "not the answer Status-Server expects" (Protocols.cs:389-391). Low. |
| 54b | radiuslab example: empty secret and password refused | WRONG | examples.txt `== radiuslab` | Both are `SecretInput` boxes that are never filled. The example is made-up output under an EXAMPLE banner. |
| 54c | decrypt example: empty code refused | WRONG | examples.txt `== decrypt` | "Code - …" is a `SecretInput` box (Toolbox.cs:212), deliberately never filled. |
| 55 | Theory: 7 sections, no Gamer Zone, 4 Toolbox sections, MAC every 30 s | OVERSTATED | docs\THEORY-OF-EVERYTHING.md:51,147,228,236 | All true (`Secs` has 9; grep "Gamer" = 0; `WatchGatewayMac` runs every 10 s). Docs only. Low. |

## Medium-Low (37)

| Muse item (short) | Verdict | Where | Evidence |
|---|---|---|---|
| "speed"/"slow" search opens iperf3/bloat, not Speed test | REAL | Ui\FullView.Toolbox.cs:514,754 | The synonym gives iperf 90, while the whole-word title match gives speedtest 70. `speedtest` is in no synonym list. |
| TWAMP has no Duration case | OVERSTATED | Ui\FullView.Toolbox.cs:455-508 | No `twamp` case, so it says "a few seconds"; the default 100 packets alone take 10 s or more. Text only. |
| httptime promises ~N s against a 5-min deadline | WRONG | Ui\FullView.Toolbox.cs:464 | The string is "about N seconds, **longer for a slow site**". Muse dropped the hedge. |
| Lab ICMP chart plots loss as stale RTT / 0 | OVERSTATED | Checks\Lab.cs:160 | True (`times[b].Last()` or 0). The summary rows are right; only the live chart is affected. |
| About: "single source file" | OVERSTATED | Ui\FullView.Settings.cs:316 | True. A leftover of round-1 #219, which was fixed on the site and CONTRIBUTING but not in-app. Cosmetic. |
| example route: 10.20.30.40 leak | OVERSTATED | examples.txt `== route` | True. Example text. |
| example wol: MAC mismatch | OVERSTATED | `== wol` | True. |
| example netflow says v9 | OVERSTATED | `== netflow` vs Lab.cs:341,362 (v5) | True. |
| example manyports: 4 of 6, "blocked" | OVERSTATED | `== manyports` vs MoreTools.cs:144 (7 mail ports) | True. |
| example dig: "Asked 1.1.1.1" with empty server | OVERSTATED | `== dig` | True. |
| example ret: Out/Back summary rows don't exist | OVERSTATED | `== ret` vs Toolkit.cs:933-961 | True. |
| example lag: legs "arithmetically impossible", % loss | OVERSTATED | `== lag` vs Guided.cs:113-115 | Loss as % vs counts is true. "Impossible" is not: the server and the internet check are different targets. It's only implausible. |
| example join: verdict direction, 0x50005 | OVERSTATED | `== join` | The verdict wording differs from `EngPack.Hint`. "0x50005 nowhere in src" isn't a defect, because reason codes are echoed from the event text (`Reason()`). |
| example dot1x: wrong row shape | OVERSTATED | `== dot1x` vs EngPack.cs:120 | The time label is `ddd HH:mm:ss` in code. Minor. |
| example winupdate: weekdays wrong for Nov 2026 | WRONG | `== winupdate` | The example has no year. Tue 4 Nov, Fri 7 Nov and Sat 1 Nov are all correct for 2025 (ran `date`). |
| example udpsend: 35 vs 39 bytes | OVERSTATED | `== udpsend` vs Lab.Payload | True: no CRLF is added, so it's 35. |
| example dnsrace omits Quad9 | OVERSTATED | `== dnsrace` vs Picks.cs:194 | True. The router address follows the documentation-range rule. |
| example ispreport: saved row name/file | OVERSTATED | `== ispreport` vs IspReport.cs:200,290 | True. |
| example selfsigned "2 years" vs 397 days | OVERSTATED | `== selfsigned` vs CertKeys.cs:187 | True. |
| example speedtest row shape | OVERSTATED | `== speedtest` vs SpeedTest.cs:119-126 | True. |
| example finddev 5 vs 4 rows | OVERSTATED | `== finddev` | True. |
| example sweep 4 of 254 vs 3 rows | OVERSTATED | `== sweep` vs Toolkit.cs:790-796 | True (code lists every answer, this PC included). |
| example rareplay 3 announcements vs 2 rows | WRONG | `== rareplay` vs Replay.cs:131 | The real output is per router with "N RA(s)", so 3 RAs from 2 routers is consistent. |
| example inventory 9 seen vs 3 rows | OVERSTATED | `== inventory` | True. Truncated sample. |
| example changed: KB0000000 | WRONG | `== changed` | A deliberate made-up placeholder under the EXAMPLE banner, the same convention as 192.0.2.x. |
| FR 'to' → 'avant' | OVERSTATED | knowledge\lang-fr.txt:312 | "3 avant Débutant" reads fine in French ("3 more before Beginner"). Stylistic. |
| FR 'Gamer Zone' → 'Zone joueur' | WRONG | lang-fr.txt:68 | A style preference, not a mistranslation. |
| FR 'Adeebo Points' → 'points Adeebo' | OVERSTATED | lang-fr.txt:287 vs :316 | Inconsistent capitalisation. Low. |
| FR 'Monitor' → 'Surveiller' | WRONG | lang-fr.txt:110 | Matches the other section tabs, which are verbs too: Investigate → Enquêter, Learn → Apprendre. |
| FR 'Sniff' untranslated | OVERSTATED | lang-fr.txt:163-164 | True. Jargon left in English. Low. |
| FR stale Points keys | WRONG | lang-fr.txt:284-299 | Muse's own grep shows no lookup, so these strings are dead and never shown. "A future translator could reintroduce" is not a defect. |
| "Start with Windows" On for a copy that won't start | OVERSTATED | AppCtx.cs:64,233 | True, but it's the intended result of the round-1 #46 fix: a copy never takes over the installed copy's entry, and AdeeboMeter (installed) does start. Low. |
| "Show game ping" toggle ignored by Gamer style | WRONG | Ui\Theme.cs:287; FullView.Settings.cs:109 | Style "gamer" is documented as "Always the gamer card". The toggle only means something for "auto", which is exactly when it's shown. |
| Docs self-test count 63/61 vs 68 | DUPLICATE | README.md:84; THEORY:281; index.html:878 | Round-1 #220 (self-test count out of step; fixcheck PARTIAL). 68 `T("…")` registrations confirmed. |
| Theory "233 terms" vs 314 | OVERSTATED | THEORY:151,232 | 314 `== ` sections (141 are `tb.*` tools). 233 is stale either way. Docs. |
| CHANGELOG Points economy stale | OVERSTATED | CHANGELOG.md:45 vs Points.cs:29,32 | True. Docs. |
| privacy.html "What stays" incomplete | DUPLICATE | docs\privacy.html:42-46 | Round-1 #244 (fixcheck PARTIAL: "privacy.html still lists none of game list, keys, Points, outage log") and R2-hof #11 (air.txt). Muse adds usage, baselines, devices, seen apps and game history: the same finding, longer list. |

## Low sample (20 of 102)

| Muse item (short) | Verdict | Where | Evidence |
|---|---|---|---|
| VpnAware aborts on a protected process | WRONG | Checks\VpnAware.cs:43 | `GetProcesses()` fills `ProcessName` from the system snapshot. Reading it doesn't open the process, so no Win32Exception. Muse marked the exception "guessing". |
| Adapter error rate negative after counter reset | REAL | Link\WifiAdvisor.cs:44,60 | No clamp. It prints "-3 a minute". |
| 4-hour row cap vs full-duration summary | REAL | Brain\Watch.cs:62,71-72 | `mins` comes from `started`; loss comes from the first 14,400 rows. |
| FileCrypt.Clean folds passphrases | WRONG | Checks\FileCrypt.cs:68 | The recovery passphrase is generated by the app (`FileCrypt.NewCode()`, Toolbox.cs:282, CertKeys.cs:341) and shown in capitals with dashes, so folding costs no entropy. |
| OpenWifi misses saved WEP auto-join | REAL | Link\LinkInfo.cs:171 | `open` needs `encryption none`, so WEP is never flagged. |
| DMARC `p=` regex matches `sp=` | REAL | Checks\SecChecks.cs:564 | True. RFC 7489's ABNF puts `p` before `sp`, so only non-conforming records trigger it. |
| Hosts: "0" not treated as a sink | REAL | Checks\SecChecks.cs:244 | True in code. Whether Windows treats "0" as 0.0.0.0 is unverified, and the trigger is exotic. |
| Journey "Capture this conversation" silent with no adapter | REAL | Ui\FullView.Journey.cs:230 | Silent `return`. |
| Debug() moves the mouse cursor | REAL | Ui\FullView.cs:672 | Deep-link/demo path only. |
| MTR overruns by up to one round | REAL | Checks\PathTools.cs:74-96 | The loop checks the time only between rounds. This duplicates Muse's own #15 secondary note. |
| UDP MTU capped at 1500 on jumbo LANs | WRONG | Checks\UdpMtu.cs:34 | The tool only probes QUIC (443) or DNS (53) servers, cloudflare.com and 1.1.1.1 by default. Jumbo frames never cross that path. |
| IPv6-only adapter labelled "static" | REAL | Link\LinkInfo.cs:562 | `p4 == null` falls to "set by hand (static)". |
| iperf3 upload UDP loss divides by received | WRONG | Checks\Toolkit.cs:316-317 | iperf3's server `packets` is `packet_count`, the highest sequence received (iperf_udp.c), so about the number sent. `errors/packets` is iperf3's own loss formula. |
| iperf3 `-6` on v4-only host: cryptic error | REAL | Checks\Toolkit.cs:201 | `First(...)` throws "Sequence contains no matching element". |
| Future-dated driver: negative age | REAL | WindowsDeep\DevInfo.cs:108 | No clamp. |
| radiuslab Duration "up to 4 s" vs ~13 s | REAL | Toolbox.cs:477; Protocols.cs:437 | TACACS+ runs 3 sequential 3000 ms connects. |
| ispreport Duration "about 30 s to trace" | REAL | IspReport.cs:71; PathTools.cs:26-33 | 24 hops × 2 tries × 1.2 s per route. |
| HealthCheck "on battery 255%" | REAL | Checks\HealthCheck.cs:97-99 | A sibling site of round-1 #145. That fix covered only PcHealth.cs. |
| Store copy: import settings opens Startup settings | REAL | Ui\FullView.Settings.cs:536; AppCtx.cs:64 | `SetAutoStart` → `StoreStartup.Open()` on any import value. |
| Dead `GameStats.StatsFor` | REAL | Gamer\Gamer.cs:345 | grep: definition only. |

## What Muse got wrong

**Mediums**
- **#9 SnmpGear FindMac.** The ARP key is `ifIndex.a.b.c.d`, so `EndsWith("." + ip)` matches exactly the right IP. Muse's "10.192.168.1.5" is ifIndex 10 for the same address.
- **#21 Journey rewind.** `Rewind()` returns nothing until the remote is known (`if (F.Remote == "") return got;`), so there are never unfiltered "before" packets.
- **#29 RouterOld.** Weight 3 deliberately means "list, don't flag" (level 3 = info). It isn't a lost "level-3 intent".
- **#30 SmbSign on 24H2.** The 24H2 default is written as `RequireSecuritySignature = 1`, so the code reads "required". The row is grey info anyway. Not checked on a live machine.
- **#42 Overview text box.** A WinForms child `Location` is in client coordinates, and `virtual + AutoScrollPosition` is the correct mapping. Muse has the coordinate model backwards.
- **#52 Icmp6 reply offset.** `IPV6_ADDRESS_EX` is packed (pshpack1), so the address is at offset 6. .NET's own interop agrees: `Pack=1`, 6-byte prefix.
- **#54 radiuslab and decrypt examples (and the premise of radius).** The empty boxes are secret/code boxes that "Fill the boxes" never fills by design (`SecretInput`). They are not "inputs the tool refuses". Only the radius example's Access-Reject wording is actually wrong.
- **Side note on #37.** "No managed API to delete the key" is wrong: `CngKey.Delete()` exists and the same codebase uses it in EcKeys.cs:86. The bug itself is real.

**Medium-Lows**
- **httptime Duration.** Muse dropped the "longer for a slow site" hedge.
- **winupdate example.** It has no year, and every weekday is right for November 2025.
- **rareplay example.** It counts RAs, not routers; 3 RAs from 2 routers is consistent.
- **changed example.** KB0000000 is a deliberate placeholder.
- **'Zone joueur'.** A style preference.
- **'Surveiller'.** Consistent with the other verb-named tabs (Enquêter, Apprendre).
- **Stale French Points keys.** Dead strings with no user effect.
- **Gamer-style widget toggle.** "Always the gamer card" is the documented meaning; the toggle is shown exactly where it applies.

**Low sample**
- **VpnAware.** `ProcessName` from `GetProcesses()` doesn't throw for protected processes.
- **FileCrypt.Clean.** The passphrase is app-generated.
- **UdpMtu 1500 cap.** The tool targets internet QUIC/DNS servers, where jumbo frames don't apply.
- **iperf3 upload loss.** `packets` is the highest sequence number, so `errors/packets` is iperf3's own formula.

**Severity inflation.** Most of the misses are severity, not existence. Eighteen of the Mediums are real but Low: Duration strings, docs, cosmetic UI, and exotic triggers (#17, #33, #34, #53). Nearly all the Medium-Lows are example-file text, docs counts or French wording.

**Duplicates.** Muse's exclusion list missed R2-calc-labs #4 and #5, R2-hof #9 and #10, R2-core-tools #10, and round-1 #220 and #244. Outside this scope, High #6 (vacuous LabToolsMissingGate self-test) is already stated in round-1 #229 ("The self-test's LabToolsMissingGate only looks at tools in 'lab' sections, so it can't catch them").
