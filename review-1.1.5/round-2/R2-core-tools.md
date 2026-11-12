# Round 2: core and tools, new bugs in code changed since f6d5747 (a0fbd56)

Scope: Program, Monitor, Setup, Policy, Report, History, Native, LinkInfo, WifiAdvisor, the changed Checks files (Toolkit, SpeedTest, SnmpGear/Tools, SysTools, VpnAware, WhyFails, WinEvidence, FileCrypt, Lab, Icmp, Investigate*, PathTools, IspReport), LiveCapture, WindowsDeep Inside/PcHealth, and the UI files Controls, FullView, Settings, Toolbox, Link, Overview, Inside, Insights, Examples, Gamer and GameEditor.
I did not repeat round-1 items, and I checked against Muse's round-2 list (`review-1.1.5/round-2/muse.md`).
The harnesses are in `scratchpad/review2/harnessR/` (`wf.cs` for the Wi-Fi parsers, `u.cs` for UrlTools and UrlToTarget, `fc.cs` for decrypt and Stop, `d.cs` for DestCheck, and `rep/` for the `--report` runs).

### 1. `--report` never exits 0 on a healthy PC: the monitor's own "On LAN / On Wi-Fi" event always turns the verdict into "couldn't tell"
Severity: wrong result
Where: src\Report.cs line 34 (Run), together with src\Checks\Investigate.Whats.cs lines 58-61 (the `around` lambda) and lines 99 and 118
How to reproduce: Use a portable copy with `portal=0` in AdeeboMeter.settings, so that the sign-in page step can't add a level-3 row. Run `AdeeboMeter.exe --report out.json --seconds 5`.
What happens: The exit code is 3 and the JSON says `"verdict": "unknown"`, while `"finding"` says "Nothing is wrong right now - ... all work". The only level-3 row is `"Around then, 20:21": "On LAN (eth0), gateway 192.0.2.1 (happened then - not necessarily the cause)"`. That event is logged by the Monitor that `--report` itself starts a few seconds earlier, so it is there on every run. The new rule `if (lv == 0 && observed.Any(level == "3")) lv = 3` treats every informational row as a step that couldn't run. "Normal for this network" (Whats line 99) does the same, and so does "Your router is quiet" (line 118, a router that ignores ping, which is common). An Intune or RMM script therefore never sees 0.
What should happen: Only "couldn't run" rows should downgrade the verdict, for example a dedicated marker or the "u" evidence kind. The "Around then" rows, baselines and quiet-router notes are informational and should not count.
Confidence: confirmed. I ran r2.exe `--report` under `xvfb-run mono`: exit=3 twice, the second time with `portal=0`, and the only level-3 row was "Around then ... On LAN".
Rule: -

### 2. dig and DNS answer compare: a DoH server URL is silently rewritten to a bare host name, so the query goes out as plain UDP port 53 instead of encrypted DoH
Severity: wrong result / security (privacy)
Where: src\Ui\FullView.Toolbox.cs line 243 (`UrlTools`) and line 671 (StartTool URL rewrite), which then reach src\Checks\Toolkit.cs line 389 (Dig, DoH branch never reached) and src\Checks\MoreTools.cs line 130 (DnsCompare)
How to reproduce: Open Toolbox › dig. Type `example.com`, `A`, and the server `https://cloudflare-dns.com/dns-query`, which is the example the box itself shows. Do the same in "DNS answer compare" with Other DNS = `https://dns.google/dns-query`.
What happens: `dig` and `dnscmp` are not in `UrlTools`, so the new "pasted URL where a host is wanted" loop replaces the box with `Toolkit.UrlToTarget(v, false)`, which gives `cloudflare-dns.com`. The box is rewritten on screen too. Dig then no longer sees `https://`. It resolves cloudflare-dns.com with the PC's DNS and sends an unencrypted UDP/53 query to that address ("cloudflare-dns.com is 104.x (looked up with this PC's DNS)"). That address may or may not answer plain DNS. dnscmp does the same with its DoH branch. A user who chose DoH so that the query isn't visible on the network gets a plaintext query to a different endpoint than the one they asked for.
What should happen: Add `dig` and `dnscmp` to `UrlTools`, or skip boxes whose label mentions DoH or `https://`.
Confidence: confirmed by reflection. `UrlTools.Contains("dig")` and `UrlTools.Contains("dnscmp")` are both False, and `UrlToTarget("https://cloudflare-dns.com/dns-query", false)` returns `cloudflare-dns.com`. I didn't run the full UI click path.
Rule: 1 (the packet sent is not the one the user asked for), and 4-adjacent (privacy)

### 3. Turkish Windows: the Wi-Fi signal is never read (netsh writes "%74"), so every Wi-Fi verdict goes grey
Severity: wrong result
Where: src\Monitor.cs line 420 (ParseWifi, `^(\d{1,3})\s*%$`), and src\Link\LinkInfo.cs around line 493, which only knows "Signal" and then falls back to ParseWifi
How to reproduce: Feed netsh `wlan show interfaces` output with `Sinyal : %74` (Turkish puts the percent sign before the number) to `Monitor.ParseWifi`.
What happens: sig = -1, while SSID, channel and BSSID are read. After the 1.1.5 "unknown signal = no verdict" change, Turkish users never get a weak-Wi-Fi verdict, a signal-drop event, the "retransmits started when signal fell" hint or the Teams-on-weak-Wi-Fi hint. The corner widget's Wi-Fi is always "unknown", and Report's `wifi_signal` is always null.
What should happen: Also accept `%\s*(\d{1,3})`.
Confidence: confirmed (reflection on r2.dll with the sample `harnessR/wifi/tr.txt`, which gives `sig=-1`). The sample is written from my knowledge of Turkish netsh, so the Turkish format itself is a strong but unverified assumption.
Rule: -

### 4. Two Wi-Fi adapters: the monitor mixes their lines. State comes from the last block, signal from the first, and SSID, BSSID and channel from the last
Severity: wrong result
Where: src\Monitor.cs lines 413-428 (ParseWifi), called from ProbeWifi at line 441. LinkInfo got `ActiveBlock` (line 414) for exactly this, but the monitor doesn't use it.
How to reproduce: Use English output with a connected built-in adapter followed by a disconnected USB dongle (`harnessR/wifi/en2.txt`) and run ParseWifi.
What happens: The result is `state=disconnected` while the PC is connected (ActiveBlock on the same text gives `connected`). The state never changes, so `WifiEvents` never logs "Wi-Fi dropped" or "reconnected". If both adapters are connected, sig comes from adapter A (first `NN%` wins) and SSID, BSSID and channel from adapter B (last wins). The pairing of BSSID and signal is then wrong, and alternating netsh order logs false roams and "channel changed" events.
What should happen: Run `LinkInfo.ActiveBlock(outp)` before `ParseWifi` in ProbeWifi too, or keep only the block of the adapter the monitor treats as primary.
Confidence: confirmed (reflection: ParseWifi on en2 gives state=disconnected; ActiveBlock then ParseWifi gives state=connected)
Rule: -

### 5. Any-language Wi-Fi: Japanese, Russian, Turkish (and other) state lines are never read, so drops are never logged. French BSSID label suspected
Severity: wrong result
Where: src\Monitor.cs line 405 (`StateKeys`, `ChanKeys`) and line 443
How to reproduce: Run the ParseWifi harness on the ja, ru and tr samples.
What happens: The results are `ja: state= ch=`, `ru: state= ch=` and `tr: state=`. With an empty state, a real disconnect, which also has no SSID and no signal, is treated as an "empty netsh answer" (line 443). After three of them the result is `HasWifi=true, WifiSignal=-1`, and nothing is logged. `NormState` also returns the raw text for 状態 "接続されました", "подключено" and "bağlandı". Separately, French netsh labels the BSSID "BSSID du point d'accès" (as far as I know, not verified on a French PC), so the exact-match `k == "BSSID" || k == "AP BSSID"` misses it. The result is `bssid=` on fr.txt, so roams are never detected on French Windows.
What should happen: Add 状態 / 接続, состояние / подключено / отключено, durum / bağlı and similar, plus a "contains BSSID" fallback. Or take state and BSSID from the WLAN API (`WlanQueryInterface`), which doesn't depend on language.
Confidence: confirmed for ja, ru and tr state and channel (reflection). Suspected for the French BSSID label, because my sample is from memory.
Rule: -

### 6. Live capture on a Wireshark without the extra fields: the first Start now ends with "Capture stopped by itself - the adapter went away" instead of restarting without them
Severity: wrong result (misleading message; a second press works)
Where: src\Capture\LiveCapture.cs line 172 (Reader, new `else if (p == ts)` branch), with the stderr handler at lines ~106-110 (`Restart(gid)`) and Restart's `g == gen` check
How to reproduce: Use an older tshark that rejects one of `extraFields` ("Some fields aren't valid") and press Start live capture.
What happens: tshark prints the error and exits at once. The stderr handler sets `extrasOk=false` and calls `Restart(gid)`, which captures `gen` and sleeps 300 ms. Meanwhile the Reader sees stdout EOF while `running` is still true and `p == ts` (the restart hasn't swapped `ts` yet). So it sets Error to "Capture stopped by itself - the adapter went away or the PC slept" and queues `Stop()`. That Stop runs `gen++`, and the delayed restart is then dropped. The comment "A restart without the extra fields swaps ts first - not this" doesn't hold, because the restart is delayed 300 ms. The user sees a false adapter error. Because `extrasOk` stays false, the next manual Start works.
What should happen: Mark the session as "restarting", for example by clearing `ts`, or set a flag before calling Restart in the stderr handler, and skip the self-stop branch while that flag is set.
Confidence: suspected (read; a timing race, very likely in the order described)
Rule: -

### 7. An IPv6-only network (no IPv4 default gateway) is now shown red: "You're not connected to any network"
Severity: wrong result
Where: src\Monitor.cs line 726 (Judge, `offline = gwAddr == "" && !vpn`), with RefreshInterfaces (lines ~305-320, which only accepts InterNetwork gateways)
How to reproduce: Use a Wi-Fi or Ethernet network that gives only IPv6 (an IPv6-only lab or mobile hotspot with NAT64/DNS64, or an enterprise IPv6-only SSID).
What happens: No adapter has an IPv4 gateway, so `Gateway == ""` and `offline` is true. The headline goes red ("Wi-Fi is off, the cable is out, or airplane mode is on"), and ISP, DNS and internet are greyed, while browsing works. DNS probes are also skipped (line 250).
What should happen: Treat an IPv6 default gateway, or any Up adapter with a global unicast IPv6 address and a route, as "connected". Reserve the offline verdict for "no Up non-loopback adapter with an address".
Confidence: suspected (read)
Rule: -

### 8. TLS, certificate, revocation, DoT and mail tools now prefer IPv6 (dual-mode `Toolkit.Tcp`). On a network with broken IPv6 they fail in 5 s where IPv4 would work
Severity: wrong result
Where: src\Checks\Toolkit.cs line 25 (Tcp). Used by Toolkit.cs lines 657 and 694, Protocols.cs lines 122, 198 and 234, CertKeys.cs line 253, EngPack.cs line 50 and MoreTools.cs line 227.
How to reproduce: Use a PC with a global IPv6 address but no working IPv6 path, which is a common router or ISP fault. Run the HTTP and TLS inspector against a dual-stack site.
What happens: `ConnectAsync(host, port)` on a dual-mode socket tries the resolver's order, which puts AAAA first. The AAAA connect hangs past the `Wait(5000)`, so the tool reports "couldn't connect" or "no TLS answer". In 1.1.4 the IPv4-only TcpClient worked. This is a regression on exactly the networks people troubleshoot.
What should happen: Try the addresses in order with a per-address timeout (about 2-3 s), or fall back to IPv4 when the first attempt times out. At the least, say "IPv6 didn't connect" and retry over IPv4.
Confidence: suspected (read; depends on Windows' address ordering)
Rule: -

### 9. Policy `history_days` with a value that isn't one of the Settings choices (2, 4, 5, 6, 8-13, 15-29) leaves the Settings row showing nothing selected, and the row locks even when the user's own value is lower
Severity: cosmetic
Where: src\Policy.cs lines 65 and 78 (accepts 1-30), and src\Ui\FullView.Settings.cs lines 42 and 133 (`Array.IndexOf(HistDayChoices, …)` returns -1)
How to reproduce: Put `history_days = 5` in AdeeboMeter.policy and open Settings › History & data.
What happens: No pill is highlighted on "Keep history for" (`cur = -1`). Also, with `history_days = 30` and a user who chose 7, the row is locked ("Set by your IT team") although the policy doesn't constrain them, so the user can't narrow it further. Rule 6 holds: nothing is switched on.
What should happen: Show the policy value as its own label, and lock only the choices above the maximum.
Confidence: confirmed (read: SetRow highlights only `i == cur`)
Rule: 6 (holds), UX

### 10. Uninstall helper is matched by `Single.Others()`, so "close the stuck one" kills it and an immediate reinstall waits on it
Severity: cosmetic / wrong result (leftover install folder)
Where: src\Program.cs line 451 (Others: `ProcessName.StartsWith("AdeeboMeter")`), used by KillOthers and Setup.StopOthers
How to reproduce: Uninstall, which leaves `%TEMP%\AdeeboMeter-remove.exe` waiting for the old process (up to 10 minutes). Start AdeeboMeter again and answer Yes to "Close the stuck one and start fresh?", or reinstall at once.
What happens: KillOthers kills the AdeeboMeter-remove helper, so `%LOCALAPPDATA%\Programs\AdeeboMeter` is never removed. StopOthers counts the helper as "another copy" and waits its full 4 s. RunningIsOlder already excludes "-remove", but Others() doesn't.
What should happen: Exclude names ending in "-remove" in Others().
Confidence: suspected (read)
Rule: -

### 11. History compaction: an unreadable existing `.csv.gz` stops the nightly Tidy for every later day and leaves a `.gz.tmp`
Severity: slow / cosmetic
Where: src\History.cs line 73 (Tidy, append-to-existing-gz path)
How to reproduce: Truncate or damage one day's `.csv.gz` (for example after a disk error) while a late `.csv` for that day exists.
What happens: `GZipStream` decompress throws InvalidDataException inside the per-file loop. The outer catch logs it, and Tidy stops, so later days stay uncompressed until the bad day ages out. `yyyy-MM-dd….csv.gz.tmp` is left behind.
What should happen: Use a per-file try/catch. On a bad old `.gz`, keep it, write the new data to a separate part, and delete the tmp file.
Confidence: suspected (read)
Rule: -

### 12. "Least crowded channel" for 5 GHz only considers 36-48 and 149-161, and may tell a European or Japanese user to switch to a channel their router can't use
Severity: cosmetic (wrong advice)
Where: src\Link\LinkInfo.cs line 243 (WlanApi.LeastCrowded)
How to reproduce: Use a 5 GHz network on channel 100 in a busy EU building, where 36-48 are crowded.
What happens: The advice is "Channel 149 is the quietest … set it to 149". 149-165 is not allowed for normal Wi-Fi in much of the EU and in Japan, and DFS channels 52-144, often the actual quiet ones, are never offered.
What should happen: Recommend only among channels already in use around (they are legal here), or say "136 / DFS" carefully.
Confidence: suspected (read)
Rule: -

### 13. `--report` with no file name exits 3, while the new doc says 4 is "the report itself couldn't be made"
Severity: cosmetic
Where: src\Program.cs line 141 (`if (ri + 1 >= args.Length) return 3;`)
How to reproduce: Run `AdeeboMeter.exe --report`.
What happens: The exit code is 3, which the header comment in Report.cs defines as "couldn't tell" about the network.
What should happen: Return 4.
Confidence: confirmed (read)
Rule: -

## Related to Muse's items (not new, but adding evidence)

- **Decrypt temp after a normal Quit (extends Muse "kill/crash still leaves the hidden plaintext temp").** Tool runs are thread-pool Tasks, and AppCtx.Quit runs `ExitThread()` without stopping them. On .NET Framework, background threads are torn down at process exit without running `finally`, so Exit from the tray during a large decrypt should leave `.<guid>.part`: hidden, NotContentIndexed, containing unauthenticated plaintext. Nothing cleans `*.part` at start (grep finds no cleanup). On Mono the `finally` did run at exit (Mono aborts threads), so this can't be confirmed here. Stop itself works: I confirmed "stopped - nothing was saved" and no `.part` left (`fc.exe stop`, 300 MB file). During decrypt the `.part` was seen at 56 MB.
- **VpnAware full tunnel (Muse: "now checks the route table").** This holds. `Routes.Read()` stores Dest in host order (`NetworkToHostOrder`), so `r.Dest == 0x80000000u` for 128.0.0.0/1 is correct.
- **Policy (Muse: Load never resets).** That matches what I found. I also found that Policy can't switch anything on: `mode` accepts only "simple", the booleans only OR-in "off", and history only lowers. `Keep()` writes the user's own value back. The one quirk is #9.

## Checked and held up

- **Native P/Invokes.** The DNS_RECORD layout in `PtrName` is right (type at 2P, data at 2P+16, on both 32- and 64-bit), and `DnsRecordListFree(…,1)` is correct. WLAN_INTERFACE_INFO stride is 532 and WLAN_PROFILE_INFO stride is 516, both after an 8-byte header. WLAN_BSS_ENTRY capability is at 88. The WlanGetProfile signature is right, with flags 0, so no plaintext key is requested and the XML is only regex-scanned. `GetBestInterface` gets the bytes in network order. The WINHTTP_PROXY_INFO layout is right, and GlobalFree is called. The DHCPCAPI_PARAMS stride is now 20/32 with DhcpCApiCleanup. GetProcessTimes handles are closed. AltGrTypes uses ToUnicodeEx flag 4.
- **Clock.Mono (Stopwatch-based).** It is used consistently for intervals in Monitor (sampler, probe, conns, procs, displaydns, netAt, gwMacSeen) and for the log rate limit. There's no `Environment.TickCount` in the changed scheduling code. The remaining `DateTime.Now` uses are wall-clock labels (lastRoam/lastSigDrop are compared with `DateTime.Now` on both sides).
- **ParseWifi on en, de, fr and es.** Signal, SSID, channel and state are read correctly (`NN %` with a space works in French). NormState is correct for connected and disconnected in de, fr, es, it, nl and pl, including "disconnecting" and "disconnesso". ActiveBlock picks the connected block.
- **FileCrypt one-pass decrypt.** Every ciphertext byte goes through LimitStream into the HMAC exactly once. `Drain()` before the tag. Constant-time compare. A padding error maps to the MAC failure. Output goes only to a CreateNew temp in the same folder, renamed after the tag check. FreeName strips Format characters and device names (now with ¹²³ and CONIN$). A Stop during encrypt or decrypt drops the temp. Decrypt never writes to the input's name, because FreeName adds " (2)" and File.Move doesn't overwrite.
- **CleanCaptures.** The base is always `DataDir\captures` (portable `Dir\data` or LocalAppData). No part comes from settings. It deletes only `*.pcap*` and then subfolders with no files. Directory.Delete on .NET Framework doesn't follow junctions.
- **Setup.** RemoveFolder deletes only the fixed `Setup.Base`. Uninstall keeps keys-ec.txt, recipients and certs. The Run value is removed only if it points at an `AdeeboMeter*.exe`. A downgrade now opens the newer installed copy. The Zone.Identifier ADS delete is harmless. The Working state can't be closed with Alt+F4.
- **Rule 1.** New outbound calls in the changed code are user-run tools (dig name resolution, Investigate's DNS servers and Windows resolver, PortalTest only when Cfg.Portal is on, gpresult is local), or background calls already listed in About (provider PTR via `PtrName` is one query instead of GetHostEntry's two; TCP 443 fallback). Breakage is #2.
- **Rule 2.** There's no `TcpListener`, `HttpListener`, `.Bind(` or `UdpClient(port)` in the added lines.
- **Rule 8.** There's no "nokia" in the added lines.
- **Rule 4.** The decrypt "Code" box is now a SecretInput (masked, kept out of URL rewrite). Generated codes are copied with CopySecret (excluded from clipboard history and cloud). IspReport redacts cell by cell before HTML-escaping, and private hop names are replaced by their (redacted) address.
- **Policy.** Combining both files works as stated: strictest wins, `mode=pro` is ignored, `history_days` takes the minimum. Import settings re-applies the policy.
- **Controls accessibility.** SwitchAccess only reads a bool. Held-key repeat guards reset on KeyUp and LostFocus. Wheel remainders are kept. The Journey and Sniff hit-tests match the painted widths (66+6+58 inside 140).
- **Other changed code.** LiveCapture's CheckLimit timer disposes itself from inside Stop2 safely. Child dumpcap processes are killed by PID with a name re-check. Sniff folders get unique names and a 200 MB cap.
- **WindowsDeep.** The Inside PID-reuse fix and its cycle break never drop a process. The PcHealth "unknown battery" fix is correct.
- **Smaller checks.** VpnAware's route-table check is correct. SnmpTools 32-bit wrap now needs a plausible port rate. Subnet calculator: contiguous-mask check, family mismatch, /31 and /32 gateway rules. SpeedTest status checks and the connection limit. `UrlToTarget` and `HostOnly` handle `[v6]:port`, bare v6 and `%zone`.
