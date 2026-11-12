# 06 - Capture, Link/Wi-Fi, Windows deep, Knowledge, Apps, Native - adversarial review

Repo: /home/user/AdeeboMeter (v1.1.4, HEAD 0965fa1). Read-only review. Static analysis, plus a fuzz harness run under mono
(the whole project compiled with `mcs -langversion:5` into A.dll and driven by reflection).
Harness: /tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review/harnessC/Fuzz.cs (run: `mono Fuzz.exe A.dll 20000`).

## Facts that answer the brief's questions

- **There is no home-made packet parser.** Capture uses Wireshark's own tools, started by full path from `%ProgramFiles%\Wireshark`
  (tshark for the live view, dumpcap for the ring buffer and sniffs, mergecap, Wireshark.exe), plus Npcap (`System32\Npcap\wpcap.dll`).
  It does not use pktmon, `netsh trace` or raw sockets. tshark does all frame decoding, so IHL, TCP data offset, IPv6 extension
  loops, VLAN tags and jumbo frames are Wireshark's job. AdeeboMeter parses only tshark's `-T fields` text output
  (`LiveCapture.Parse`, `Packets.Read`), `-V` text (`Decode`, `ParseSwitch`) and Native WiFi IEs (`WlanApi.ReadIes`). All were fuzzed.
- **"No admin" vs. reality:** the app itself needs no admin. Npcap has to be installed (which needs admin once). If Npcap was
  installed with "Restrict driver access to Administrators", capture fails and the tshark stderr is shown. That is honest enough.
- **Leftover state after a crash or kill:** child tools are placed in a Job object with KILL_ON_JOB_CLOSE (`Program.cs:286-304`),
  so dumpcap and tshark die with the app, even after a crash or a kill. No system-wide state is left behind (no pktmon or netsh trace).
  Capture **files** do stay behind (see H-1).
- **Wireshark launch:** `ProcessStartInfo(ws, "\"" + final + "\"")` with UseShellExecute=false. `final` is built from
  `SafeCapturePath` plus a letters/digits-only app name. BPF and display filters are built from `IPAddress.TryParse` output and
  ints (`FullView.cs:826-829`), so no argument injection is possible. If Wireshark is missing, the feature is disabled up front
  (`Available`) or the open step is skipped (`File.Exists(ws)`).
- **M365.cs contacts nothing.** It labels existing TCP-table rows from a built-in host and range list. This is consistent with the
  "no telemetry" claim.
- **Native struct layouts checked and correct on x64 and x86:** WLAN_INTERFACE_INFO (532), WLAN_BSS_ENTRY (360; offsets 4/40/56/92/352/356),
  MIB_IPNETROW (24), DNS_RECORD data at 2P+16, ENUM_SERVICE_STATUS_PROCESS (56/44; state at 2P+4, pid at 2P+28),
  PROCESSENTRY32W, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, IO_COUNTERS. GetExtendedTcp/UdpTable uses a 3-try +4 KB grow loop
  (`Monitor.cs:815-827`) and GetIpNetTable uses +1 KB slack with a graceful empty result. AllocHGlobal always has FreeHGlobal in
  finally. The one layout bug is DHCPCAPI_PARAMS on 32-bit (L-6).

---

## CONFIRMED findings

### H-1 (High) "Deleted on exit" is not true for the live-capture ring and sniff work folders. Full-payload captures of all traffic stay on disk after a crash, kill, power loss or logoff
- `Program.cs:162-165`
```csharp
public static void CleanCaptures()
{ try { foreach (var f in Directory.GetFiles(CaptureDir, "*.pcapng")) try { File.Delete(f); } catch { } } catch { } }
```
- `LiveCapture.cs:62,115`: the ring is `captures\ring\ring_NNNNN_*.pcapng`, 5 x 20 MB. `LiveCapture.cs:360`: sniff work folder `captures\sniff_HHmmss\`.
- `docs/THEORY-OF-EVERYTHING.md:364` claims: "`captures\` ... live-capture ring and sniffs - deleted on exit".
- **Scenario:** live capture is running (the ring records *every* packet of this PC, unfiltered, with payload: DNS, plaintext HTTP,
  SMB, LDAP and so on). Then the app crashes, is ended from Task Manager, the PC loses power, or Windows ends the session at
  shutdown or logoff before `Stop2` runs. The Job object kills dumpcap, but nothing deletes the files. At the next start,
  `CleanCaptures()` (`AppCtx.cs:66`) is not recursive, so `ring\*.pcapng` (up to ~100 MB) and any `sniff_*\` folder
  (z_after.pcapng and partial copies) stay on disk. The ring files are removed only if the user starts Live capture again
  (`LiveCapture.cs:114`). A user who never opens that tab again keeps ~100 MB of private traffic in `%LOCALAPPDATA%` indefinitely.
- **Fix:** make CleanCaptures recursive (`Directory.GetFiles(CaptureDir, "*.pcapng", SearchOption.AllDirectories)` plus delete
  `sniff_*` folders), and also call it from `SystemEvents.SessionEnding` / WM_ENDSESSION. Consider `FileOptions.DeleteOnClose`
  for nothing else, since dumpcap owns the files.

### M-1 (Medium) Sniff captures have no size limit, only a time limit. One busy flow can write gigabytes to %LOCALAPPDATA%
- `FullView.cs:846` `dumpcap -i ... -f "<flow>" -a duration:" + Cfg.SniffSec + " -w file -q`. SniffSec is 30 or 60 (`Program.cs:243`).
- `LiveCapture.cs:366` forward capture `-a duration:15` without `-a filesize:`. Then up to 5 ring copies (`File.Copy`) are made,
  plus tshark-filtered parts and a mergecap output. At peak, that is several copies of the same data at once.
- **Scenario:** the user clicks Sniff on a large download, an OneDrive sync or an SMB copy on a 1 GbE / 2.5 GbE / Wi-Fi 7 link.
  At 1 Gb/s x 60 s that is about 7.5 GB. With SniffWithHistory, 15 s at 1 Gb/s is about 1.9 GB, plus the 100 MB ring copied,
  plus the merged file. A small SSD fills up, and Windows Update and Outlook OST start failing. Disk-full errors from dumpcap are
  only shown as "Capture failed".
- **Fix:** add `-a filesize:200000` (kB) to every dumpcap. Use `-s 256` (snaplen) for sniffs where headers are enough. Check free
  space before starting.

### M-2 (Medium) The Wi-Fi page reads only English netsh labels. On non-English Windows it shows nothing and says "Your Wi-Fi is in great shape"
- `LinkInfo.cs:368-373, 415-418, 430` use the keys `"Channel"`, `"Signal"`, `"Band"`, `"Radio type"`, `"Authentication"`,
  `"Cipher"`, `"Connected Stations"`, `"Channel Utilization"`, `"Receive rate (Mbps)"`. `Blocked()` (`:345`) looks for the English
  words "location" and "permission". The same applies to `Monitor.cs:392` (`k == "Signal"` sets `HasWifi = sig >= 0`).
- **Scenario:** German Windows uses "Kanal" and "Funktyp", French "Canal" and "Type de radio", Spanish "Canal" and "Señal",
  Japanese and Russian use fully localized labels. Then `ch = -1`, so the whole block under `if (ch > 0)` (`:435`) is skipped:
  no neighbour analysis, no WlanApi width or 11k/v/r, no band advice. WifiAdvisor gets `my = -1` and gives no advice.
  `LocationBlocked` stays false even when location is off. Where "Signal" is also localized ("Señal", "Сигнал"), `sig = -1`, so
  no weak-signal note is added. The verdict then falls through to
  `"Your Wi-Fi is in great shape - strong signal, clean air, quick router."` (`:551`), a positive verdict built on zero data.
  On non-English PCs with location off, the user is told Wi-Fi is fine and is never told to enable location.
- **Fix:** take channel, RSSI, band and SSID from the Native WiFi API (`WlanQueryInterface` wlan_intf_opcode_current_connection
  and the BSS list already parsed in WlanApi), which is language-neutral. If netsh must be used, match by line position or use
  `chcp 437`-style tricks (they do not change the language). At the least, never say "great shape" when `sig < 0 && ch < 0`.

### M-3 (Medium) The docs claim the advisor and look-alike detection work "without location". The code returns before both
- `WifiAdvisor.cs:77-81`
```csharp
if (s.LocationBlocked) { x.Advice.Insert(0, LocationOff); return x; }
```
- `docs/THEORY-OF-EVERYTHING.md:226`: "**Wi-Fi without location**: a health score ...; an advisor (quieter channel, better band,
  sticky access point, Wi-Fi power saving); look-alike networks; the walk test." README:29 makes the same kind of claim.
- With location off, only the health score, the power-saving advice and the walk test work. Channel, band, sticky-AP and twin
  detection need `netsh wlan show networks` or `WlanGetNetworkBssList`, and both are gated by location (the code itself maps
  rc 5 to "Windows needs location access", `LinkInfo.cs:160`).
- **Fix:** correct the docs, or show "needs location" placeholders for those cards.

### M-4 (Medium) Look-alike ("evil twin") warning uses the first-3-bytes OUI. This gives false security alarms on enterprise, mesh and multi-SSID networks
- `WifiAdvisor.cs:113-116, 121`
```csharp
string myOui = Oui(s.MyBssid);
foreach (var a in aps.Where(a => a.Ssid == ssid && Oui(a.Bssid) != "" && Oui(a.Bssid) != myOui && !Arp.Randomised(a.Bssid)))
   x.Twins.Add(... "a different maker's hardware ... Don't sign in to anything on it.");
```
- **Scenarios:** (1) Cisco, Aruba, Ubiquiti and Meraki own many OUIs. A campus bought in several batches has APs of the same SSID
  under different OUIs, so every one is flagged as a fake AP. (2) Many APs give secondary SSIDs a locally-administered BSSID. If
  *your* AP's BSSID is LAA (`myOui` = "x2:..."), every genuine universally-administered AP of your SSID is flagged. The filter only
  checks the *other* BSSID's LAA bit. (3) 6 GHz and MLO radios often use derived or LAA BSSIDs.
- **Fix:** skip the check when `MyBssid` is LAA. Compare vendor *names* through an OUI to vendor table, or compare
  security/auth type and RSN capabilities instead. Soften the wording to "unrecognised hardware" rather than "Don't sign in".

### M-5 (Medium) Live capture keeps saying "capturing" after tshark dies (adapter reset, sleep/resume, Wi-Fi drop, Npcap error)
- `LiveCapture.cs:146-153`
```csharp
while (running && (line = p.StandardOutput.ReadLine()) != null) { ... }
...
if (!running) ThreadPool.QueueUserWorkItem(delegate { Stop(); });
```
- When tshark exits on its own, `ReadLine()` returns null while `running` is still true, so `Stop()` is never queued. The 15-minute
  limit is checked only when a line arrives, so it never fires. `Snapshot` shows
  `"capturing 47:12 of 15:00"` (`:313`) with zero packets forever. The ring dumpcap can be dead too (RingActive false), so Sniff
  silently switches to the other path. `ring.BeginErrorReadLine()` has no handler, so ring errors (disk full, adapter gone) are
  discarded.
- **Fix:** after the loop, if tshark exited and `running` is true, set Error from its exit code or stderr, and Stop or auto-restart
  on the current adapter. Also apply the time limit in `Snapshot`.

### M-6 (Medium) WMI disk-health query has no timeout. A broken or slow WMI Storage provider hangs the toolbox action
- `PcHealth.cs` Disk(): `new ManagementObjectSearcher(scope, new ObjectQuery("SELECT ... FROM MSFT_PhysicalDisk"))` followed by
  `s.Get()`, with no `Options.Timeout`. `SecChecks.cs:46` sets `Timeout = 10 s`, so this one is inconsistent.
- **Scenario:** the Storage Management provider is known to block for a long time with sleeping USB disks, iSCSI or Storage Spaces
  rebuilds, or a corrupt WMI repository. The "Disk" tool thread never returns and the result card stays "running". Each new press
  starts another stuck thread.
- **Fix:** `s.Options.Timeout = TimeSpan.FromSeconds(10); s.Options.ReturnImmediately = true;` and run it on its own task with
  `Wait(15000)`.

### L-1 (Low) Two Sniffs in the same second share a work folder, and one run deletes the other's files
- `LiveCapture.cs:360` `SafeCapturePath("sniff_" + DateTime.Now.ToString("HHmmss"))`, and in `finally` `Directory.Delete(dir, true)`
  (`:386`). The UI flag `sniffing` blocks a second Sniff from the same window. But the non-ring Sniff, Learn, Travel
  (`journey-HHmmss`) and the final `adebometer_<app>_HHmmss.pcapng` names are all second-resolution. Two flows of the same app
  sniffed one after the other within a second overwrite each other, and Wireshark may hold the first one open, which makes
  mergecap fail with "Merging failed". The same time of day on another day is not an issue because files are wiped on exit.
- Also, `Directory.CreateDirectory(dir)` (`:360`) runs outside the try, and an exception there faults the Task without any status update.
- **Fix:** add `Guid.NewGuid().ToString("N").Substring(0,6)` or milliseconds to the names, and move CreateDirectory inside the try.

### L-2 (Low) The non-ring Sniff path runs dumpcap in promiscuous mode, which contradicts the "your own PC's traffic only (no promiscuous mode)" comment
- `FullView.cs:846`: the args lack `-p`. The ring and live paths use `-p` (`LiveCapture.cs:94,115`, `Packets.cs:39`). The BPF
  host/port filter limits what is saved, but the NIC is put into promiscuous mode, which can be visible to switch port-security or
  NAC and to IT. FindSwitch is intentionally promiscuous and documented as such in its comment.
- **Fix:** add `-p`.

### L-3 (Low) The tshark stderr check matches the substring "valid" and permanently disables RTP/DHCP extras for the whole process
- `LiveCapture.cs:107`: any stderr line containing "valid", "unrecognized" or "heuristic" (for example "...Invalid argument",
  "... is not a valid interface") sets the static `extrasOk = false`. Call-quality (MOS) and DHCP-server detection then stay off
  until the app restarts, even though the error had nothing to do with the field list.
- **Fix:** test for tshark's actual message ("Some fields aren't valid" / `-e` field name) or check the exit code.

### L-4 (Low) BandOf guesses the band from the channel number. 6 GHz channels are reported as 2.4 or 5 GHz when netsh has no `Band :` line
- `LinkInfo.cs:346-350`. Harness output:
  `1->'2.4 GHz' | 5->'2.4 GHz' | 13->'2.4 GHz' | 37->'5 GHz' | 53->'5 GHz' | 101->'5 GHz' | 149->'5 GHz' | 181->'' | 233->''`.
  6 GHz channels 1-233 overlap both ranges. Older Win11 builds without the `Band` field mislabel a 6E AP, and the advisor then
  recommends 2.4 GHz channel 1/6/11 or a "5 GHz channel" change for a 6 GHz BSS.
- `WlanApi.FromFreq` (`:191`): 5935 MHz (6 GHz channel 2) gives ch -3, and 5925 gives ch -5. The 4.9 GHz band (JP ch 184-196)
  gets no band. Harness: `5935MHz->6 GHz ch-3 | 5925MHz->6 GHz ch-5 | 4920MHz-> ch0`.
- `Overlaps()` for 5 and 6 GHz is `a == b`, which ignores 40/80/160 MHz bonding. A neighbour at 80 MHz on channel 36 does share
  channel 40.
- **Fix:** prefer WlanApi's frequency over BandOf (match by BSSID), special-case 5935, and use the width when testing overlap.

### L-5 (Low) Startup list: one Run value with an illegal path character hides that entry and every later value in the same key
- `Inside.cs:146,170-175`: `ExeOf()` calls `Path.GetFileName(exe)`. On .NET Framework 4.8 that throws `ArgumentException`
  ("Illegal characters in path") for `" < > |` and control chars. The exception is caught by the per-key `try { ... } catch { }`
  around the *whole* `foreach`, so the current and all following values of HKCU\...\Run are silently dropped.
  Harness: 1017 ArgumentExceptions from random input (Mono flags fewer characters than .NET, so .NET would throw at least as often).
- **Scenario:** the Run value `C:\Users\x\AppData\evil.exe"` (stray quote) or one with a tab hides itself and the entries after it
  from "what starts with Windows". This is security-relevant because the page exists to show persistence.
- **Fix:** wrap ExeOf in try/catch and return the raw token, or strip invalid chars first.

### L-6 (Low) DHCPCAPI_PARAMS array stride is hard-coded to 32 bytes, which is wrong on 32-bit Windows
- `Lab.cs:225-228,237` (P/Invoke for `Native.DhcpRequestParams`): `const int sz = 32` is used as the stride for both bitnesses. The
  per-field offsets are bitness-aware (12/16 vs 16/24), but the x86 struct is 20 bytes. The build is AnyCPU via csc, so it runs
  32-bit on 32-bit Windows 10. Elements 1 and later are misplaced, and Windows reads garbage OptionIds and Data pointers from
  uninitialized AllocHGlobal memory. The results are garbage, and in the worst case the API writes through a garbage pointer.
- **Fix:** `int sz = IntPtr.Size == 8 ? 32 : 20;`. Also zero the buffer.

### L-7 (Low) ProcTree: "a parent must also be older" is stated in a comment but not implemented. PID reuse creates cycles, and those processes disappear from the tree
- `Inside.cs:24,39-44`: the parent is linked purely by `th32ParentProcessID`. With PID reuse, A(pid 100, parent 200 dead) and a new
  B(pid 200, parent 100) form a cycle. Neither is a root, and `walk` starts only from roots, so both vanish from the "who started
  whom" view. A process can also be wrongly attributed to an unrelated new process (for example "Started by explorer.exe").
  `M365.FamilyByPid` has the same misattribution, guarded to 6 hops.
- **Fix:** get creation times (`GetProcessTimes`) and link only if parent.CreateTime <= child.CreateTime. Treat anything unreached
  after the walk as a root.

### L-8 (Low) M365.Role labels hosts by substring, so arbitrary domains get "Sign-in" or "Signalling" labels
- `M365.cs:31-32` `if (host.Contains(h[0]))`. Harness: `msauth.evil.example -> Sign-in`, `notskype.com.attacker.net -> Signalling`,
  `trouter.example.org -> Signalling`, `vortex-gaming.example -> Telemetry`, `myoffice.com.example.net -> Office`.
  The host comes from the DNS cache or SNI, so a phishing or tracker domain is presented as a Microsoft role.
- **Fix:** match on label boundaries (`host == d || host.EndsWith("." + d)`).

### L-9 (Low) "on battery 255%" on machines that report an unknown AC line status
- `PcHealth.cs` Pressure(): `ps.PowerLineStatus == Online ? "plugged in" : "on battery " + (ps.BatteryLifePercent*100)`.
  Desktops and VMs with ACLineStatus=255 (Unknown) and BatteryLifePercent=255 show "on battery 255%".
- **Fix:** check `BatteryChargeStatus.NoSystemBattery` and `Unknown`, as Battery() already does.

### L-10 (Low) Parser robustness, from the fuzz run (all caught higher up, but each one aborts the whole page or row set)
- `LinkInfo.ParseNetworks` raises `OverflowException` from `int.Parse` on `Channel Utilization: 1 (99999999999 %)` (`:373`), 130 hits.
  The exception is not caught in `Collect`, so the whole "Your connection" snapshot is lost (caught and logged at `FullView.Link.cs:40`).
  Fix: use `int.TryParse`.
- `Arp.Randomised` raises `FormatException` or `ArgumentException` on non-hex input (`LinkInfo.cs:253`), which is reached from
  `WifiAdvisor.Fill` with netsh BSSIDs, so Fill throws and the advisor is lost. Fix: use TryParse.
- `EvLog.Plain(null)` and `Knowledge.Parse(null)` raise NRE. Current callers never pass null (informational).
- `M365.InRange` raises IndexOutOfRange on a CIDR without "/". Only constants are used today (informational).
- `ExplainPaint.Paint` indexes `CloseText[e.Close]` (`Explain.cs:115`). A knowledge entry with `close: 4` or more (the files invite
  community edits) throws inside a Paint handler. Today all values are -1..3 (checked: 21/24/16/11 in processes.txt, -1 in terms).
  Fix: clamp.
- Knowledge files: no duplicate keys, LF only, and every entry has all fields. Continuation lines (`"  "`) are silently ignored by
  `Parse` (`Knowledge.cs:66`) and none are used today, but a contributor who wraps a long `what:` line loses the text without warning.

### L-11 (Low) Other small items
- `FullView.cs:851`: the status text says "Listening in for 30 s" even when SniffSec = 60.
- `Packets.Capture` (`Packets.cs:38`) checks `file == null`, but SafeCapturePath never returns null (it throws), so the check is dead.
- An IPv6 link-local remote with a scope (`fe80::1%12`) makes an invalid BPF and display filter in Sniff, and `SafeFilter` rejects
  `%` in Travel ("Bad filter.").
- `Battery()` uses a fixed `%TEMP%\adeebometer-battery.xml`. Two concurrent runs (toolbox plus Investigate) delete each other's file.
- Multiple Wi-Fi adapters: `Fields()` keeps the first occurrence of each key across all interface blocks, so the values of two
  adapters can be mixed.
- netsh output is decoded with the default encoding (Job.Run sets UTF-8 only for tshark). Under .NET Framework that is
  `Console.OutputEncoding`, which is the ANSI code page in a console-less WinForms app, while netsh writes OEM. Non-ASCII SSIDs
  (é, ü, CJK, emoji) are mangled in the UI. Matching still works because both netsh calls are mangled the same way.
- The 5 GHz advice can recommend channels 149-165, which some regulatory domains restrict, and 165 is 20 MHz-only.

---

## SUSPECTED (needs a Windows box to confirm)

- **S-1** French or other locales may print the signal as `85 %` with a non-breaking space (U+00A0 or OEM 0xFF). `TrimEnd('%',' ')`
  (`LinkInfo.cs:368,416`) would then not strip it, giving `sig = -1`, so the weak-signal logic is skipped on those locales.
- **S-2** On Windows 11 24H2 with location off, `netsh wlan show interfaces` itself is gated. `Monitor.ProbeWifi` then sets
  `HasWifi = false`, and the widget may treat a Wi-Fi PC as having no Wi-Fi. The health score still works (it uses pings), but the
  SSID and signal rows show "-".
- **S-3** `tshark` launches its own dumpcap child. If it is spawned before `Job.Add(ts)` (a race of a few ms), that dumpcap is not
  in the job and survives a crash. It is probably killed when its pipe write fails, but on an idle link that may not happen for a
  long time.
- **S-4** `Stop2` kills tshark with `Process.Kill`, not the tree. tshark's dumpcap child lives until its next write fails or the
  app exits (it is in the job). While the link is idle, a stray dumpcap may hold the adapter.
- **S-5** `GPU Engine`: up to 400 PerformanceCounter instances are created per press. On machines with many GPU processes this
  takes several seconds and many registry/PDH calls.

---

## Fuzz results (mono, 20 000 iterations per target, seed 12345)

| Target | Result |
|---|---|
| LiveCapture.Parse (tshark fields line, 14-24 random fields incl. huge ints, commas, IPv6 scope ids, NUL, emoji) | **0 exceptions**; 15 127 lines correctly rejected as malformed (`Dropped`) |
| LiveCapture.AddRtp (seq wrap, garbage seq/ts, time jumps of ±1e6 s) + Snapshot | 0 exceptions |
| LiveCapture.ParseSwitch (LLDP/CDP text) | 0 |
| WlanApi.ReadIes (40 000 random IE blobs biased to HT/VHT/HE/RM/MD/ExtCap ids) | 0 (bounds checks hold) |
| LinkInfo.Fields | 0 |
| LinkInfo.ParseNetworks | **OverflowException** (int.Parse of a huge Channel Utilization %), 130 hits |
| Monitor.ParseDisplayDns | 0 |
| Lab.DhcpText (all option ids, random and low-value bytes, incl. 119 compression and 121 classless routes) | 0 |
| Knowledge.Parse | 0 (NRE only on null) |
| EvLog.Field / ReasonOf / Plain | 0 (NRE only for Plain(null)) |
| Startup.ExeOf | **ArgumentException** (1017 of 5000 random inputs; illegal path chars) |
| Arp.Randomised | FormatException / ArgumentException on non-hex |
| M365.InRange | IndexOutOfRange on a CIDR without '/' |
