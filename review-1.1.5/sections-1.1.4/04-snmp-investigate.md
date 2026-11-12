# 04 - SNMP, routing, path, Investigate, system tools: adversarial review (AdeeboMeter v1.1.4)

Scope: src/Checks/Snmp.cs, SnmpGear.cs, SnmpTools.cs, RoutingTools.cs, PathTools.cs, SysTools.cs, Investigate.cs,
Investigate.Whats.cs, WhyFails.cs, HealthCheck.cs, Inspection.cs, WinEvidence.cs and src/Ui/FullView.{Insights,Story,Replay,Travel}.cs.
I did not edit the repo. Everything in "Confirmed" was either run under mono 6 against the compiled assembly (`A.dll`,
harnesses in `harnessB/`) or follows unambiguously from the code. "Suspected" depends on Windows/device behaviour
that I could not run here.

Harness files (scratch):
- `/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review/harnessB/Fuzz.cs` - BER/ASN.1 fuzzer (reflection into Snmp.Parse2/ReadPdu/Parse3/Head3/ReportName/OidText/Text/Num)
- `.../harnessB/Agent.cs` - a malicious SNMP agent on 127.0.0.1 driving the real `Snmp.Walk`
- `.../harnessB/Misc.cs` - Icmp.Resolve4, history TimeSpan parsing, Traffic wrap maths, Ip46 and MtrVerdict logic

---------------------------------------------------------------------------------------------------------------------

## Fuzz results (SNMP decoder)

**No crash and no hang in 330,000 malformed packets.** I ran 300,000 random or mutated v2c packets through
Parse2, Head3, ReportName, OidText, Text and Num, and 30,000 mutated v3 packets through Parse3 (noAuth and
auth+priv credentials). Mutations: random bytes; length bytes set to 0x80, 0x81, 0x82, 0x83, 0x84 or 0xFF;
truncation; insertion. The slowest single input took 3 ms. Hand-made cases:

| case | result |
|---|---|
| `30 84 FF FF FF FF` (4-byte length), `30 83 FF FF FF`, indefinite `30 80` | rejected: "not an SNMP reply" (`T()` allows at most 3 length bytes, and pos+hl+len is bounded) |
| truncated TLV, empty packet, a single byte | rejected cleanly |
| 5,000 nested SEQUENCEs | 0 varbinds, 0 ms (the decoder is iterative, so nesting does no harm) |
| OID with an arc above 2^32 | the varbind loop stops (OidText returns "") and the reply is accepted with 0 varbinds |
| OID whose last arc is cut off (`2b 06 01 81`) | **reads silently as `1.3.6.1`**: the dangling byte is dropped |
| OID first byte 0xFF | reads as `2.175.1` (no long-form first arc; harmless) |
| Counter64 with 9 bytes `01 FF..FF` | **reads as 18446744073709551615**: the top byte is shifted out without any error |
| INTEGER with 9 bytes (`01 00..05`) | **Text() shows "0"** (ReadInt returns 0 when l > 8), while Num() returns 5 |
| empty INTEGER | **Text() shows "0"** and Num() returns 0 |
| error-status as an empty INTEGER or as 9 bytes, with a real error inside | **the reply is accepted as success**, because ReadInt gives 0 |
| request-id encoded as 9 bytes | rejected ("a reply to a different request") |
| TimeTicks with 5 bytes FF | accepted, "127258 d 6 h 57 min" (TimeTicks is 32-bit; a 5th byte should be refused) |
| IpAddress with 5 bytes | "(bad address)" (fine) |

Live malicious-agent test, using the real `Snmp.Walk`, max 2000, with a 30 s deadline in place of the real 120 s:

| agent behaviour | result |
|---|---|
| always answers `.10.1, .10.2`, whatever was asked (the walk loops back) | **1000 requests, 2000 "values" with only 2 distinct OIDs, error "" (a clean finish)** |
| answers an earlier OID each time (`.1, .2, .3, .1...`) | **runs until the deadline: 1460 requests, 3 distinct OIDs**, then "stopped at the 2-minute limit" |
| wrong request-id | rejected right away |

---------------------------------------------------------------------------------------------------------------------

## CONFIRMED

### C1. High - Find a MAC (and IP to MAC) reports "not in this switch's MAC table" when the table has more than 2,000 entries
`SnmpTools.cs:49` (Col) together with `Snmp.cs:348-365` (Walk) and `SnmpGear.cs:75-94`
```csharp
var d = new Dictionary<string, Snmp.Vb>(); var vbs = Snmp.Walk(ep, c, oid, 2000, run, out err);
if (err != "" && partial == null && (vbs != null || ...)) partial = err;
...
if (hits.Count == 0) { rows.Add(R(1, mac, "not in this switch's MAC table (" + all.Count + " entries)", ...
```
When Walk reaches `max` it returns with `error == ""`. Col therefore never sets `partial`, so no "Partial result"
row appears. A distribution or core switch with 5k-50k FDB entries gets only the first 2,000 read (and they are in
VLAN/MAC order). Any MAC beyond that is reported as "not in this switch's MAC table (2000 entries)", with the advice
"Switches forget a MAC after about 5 minutes... or it's behind another switch". That is a wrong answer given with
confidence. The same happens to IP to MAC on a router whose ARP table has more than 2,000 entries ("this device
doesn't have it in its ARP table"), and to every Col-based view on large tables (Vlans, Lag, Interfaces with more than
2,000 ifIndexes).
Fix: Walk should report a cut at `max` (`error = "stopped at " + max + " values"`), and Col should treat it as partial.
For FindMac with a known MAC, ask for the instance directly (`GET dot1qTpFdbPort.<vlan>.<mac>`, or walk per VLAN
with the MAC as the start OID) rather than reading the whole table.

### C2. Medium - Walk accepts OIDs that go backwards or loop, so a broken or malicious agent burns the full 2-minute budget and gets back duplicate data reported as complete
`Snmp.cs:355-361`
```csharp
if (v.End || !v.Oid.StartsWith(pre, StringComparison.Ordinal)) { return all; }
if (string.CompareOrdinal(v.Oid, cur) == 0) continue;
all.Add(v); cur = v.Oid; moved = true;
```
Only *equality* with `cur` is checked. No check makes sure the OID is lexicographically greater (by number, not by
ordinal string). Fuzz agent: 2000 rows from 2 distinct OIDs with no error, or the walk keeps hammering the device
until the deadline (about 50 requests per second, with a 20 ms pacing sleep). Real devices do this too: buggy printer
and old Cisco agents return OIDs that do not increase, which is why Net-SNMP has "OID not increasing". After Col
removes duplicates the user sees a truncated table with nothing marked partial (raw Walk: "2000 values (stopped at
2,000)").
Fix: compare OIDs as uint arrays and abort with `error = "the device returned an OID that isn't after the last one
(<oid>) - stopped"` whenever `Compare(v.Oid, cur) <= 0`. Also keep a HashSet of seen OIDs.

### C3. High - "What's wrong?" concludes "Your router doesn't answer at all" and never tests the internet when the gateway drops ICMP and TCP 53/80/443
`Investigate.Whats.cs:69-80`
```csharp
if (double.IsNaN(gw) && g != null) foreach (var p in new[] { 53, 80, 443 }) { gwTcp = TcpReach(g, p, 800); ... }
...
if (g != null && double.IsNaN(gw) && double.IsNaN(gwTcp))
{ r.Conclude(2, "Your router (" + g + ") doesn't answer at all - the problem is right next to you: the ... cable or the router itself."); ... return; }
```
Scenario: a corporate L3 gateway or firewall VIP that drops ping and has no management service on the user VLAN
(very common: ASA/FTD, HSRP VIP with an ACL, many cloud/hotel gateways). The internet works perfectly. The step returns
before step 4, so the user is told to check the cable and restart the router. `Slow()` is worse
(`Investigate.cs:86-88`): it has no TCP fallback at all, and `double.IsNaN(gw)` alone gives "Your router isn't
answering at all". Replay (`FullView.Replay.cs:150`, `lanLv = double.IsNaN(gw) ? 2`) and the Story router box
(`FullView.Story.cs:74`) paint the router red in the same situation.
Fix: when the gateway is silent, still run step 4. If the internet answers, conclude "the router doesn't answer
pings (normal for some routers) - the path works", and use `Monitor.GwEverReplied` to tell "never answers ping" apart
from "stopped answering".

### C4. High - Captive portal is diagnosed as "provider / modem" because step 4 returns before the sign-in-page check (step 6)
`Investigate.Whats.cs:84-94` against `111-125`
```csharp
if (double.IsNaN(net) && double.IsNaN(netTcp))
{ r.Conclude(2, "Your router answers, but nothing beyond it does - the break is between your router and the internet: your provider, the modem, or ..."); ... return; }
```
Before sign-in, a hotel or airport portal usually drops ICMP and TCP 443 to 1.1.1.1 and 8.8.8.8 and only hijacks HTTP
80 and DNS. Both tests in step 4 fail, so the run ends with "restart the modem / call your provider". The msftconnecttest
probe that would have found the portal never runs.
Fix: run the `http://www.msftconnecttest.com/connecttest.txt` probe (or `GetIsNetworkAvailable` plus the NCSI state)
before concluding in step 4. A 3xx or 200 with the wrong body means a portal.

### C5. High - "Works at home, not on VPN" says "looks fine: the door opens and full packets get through" (green) when the door was dropped, if the target is a URL without a port
`Icmp.cs:100` (used by `Investigate.cs:257`) and `Investigate.cs:255-268`
```csharp
int c = h.LastIndexOf(':'); if (c > 0 && h.IndexOf(':') == c) h = h.Substring(0, c);   // "https://x/app" -> "https"
...
var dst = Icmp.Resolve4(target);
if (dst != null) { ... if (pt[0] == "2" && verdict == "") { verdict = "The door ... is silently dropped"; lv = 2; } }
...
if (verdict == "") { verdict = ... "The path to " + target + " looks fine from here: the door opens and full packets get through. ..."; lv = 0; }
```
Run under mono: `Resolve4("http://10.0.0.5")` returns null and `Resolve4("https://192.168.1.10/app")` returns null.
In a URL the only colon is the scheme's, so the "host:port" stripping turns the input into `"https"`. With the input
`https://intranet.corp/app`, `WhyFails.Port` connects to intranet.corp:443 and times out (lv 2 observation). But
`dst == null` skips the whole block that holds the door verdict and the MTU test, and the conclusion is the green
"door opens and full packets get through". `RoutingTools.RouteFor` and the MTU/trace tools that take a URL fail the
same way ("couldn't find an IPv4 address").
Fix: in Resolve4, handle the `http` scheme before stripping the port. In Vpn(), base the door verdict on `pt[0]`
whatever `dst` is, and never write "full packets get through" unless MtuAt actually ran.

### C6. Medium - "weak-but-working never says all clear" does not hold: Slow() and What's wrong? both give green with amber observations
- `Investigate.cs:94`. Slow() records amber observations for router 16-50 ms, internet 81-150 ms, loss 1-3 %, DNS
  121-300 ms, signal 35-39 % or bloat 21-80 ms, then concludes `Conclude(0, "Nothing on the path is slow right now")`,
  because each branch only reacts to the red thresholds.
- `Investigate.Whats.cs:86`. Internet loss is amber only above 5 %, so 2-5 % loss (robotic calls) ends in "Nothing is
  wrong right now". The router is amber only above 60 ms, so 16-60 ms on a LAN is green. Wi-Fi is amber only below
  30 %, so the "weak" scan at line 127 never sees 30-39 %. The thresholds also disagree with Slow() (1 %, 15 ms, 35-40 %).
- After a retest (`retested == true`) the loss is not shown and does not count.
Fix: in both flows, if any observation has `lv >= 1`, conclude at least 1 ("Nothing is broken, but X is borderline")
and share one set of thresholds.

### C7. Medium - Traffic on 32-bit counters turns a device reboot or counter reset into a huge phantom rate
`SnmpTools.cs:168-172`
```csharp
if (q < p && hc) return -2;
ulong d = q >= p ? q - p : (uint.MaxValue - p + q + 1); return d * 8.0 / dt;   // a 32-bit counter can wrap once
```
Without HC counters, any decrease is taken as a wrap. Computed with p=4.0e9 and q=1000 (a reset): 294,968,296 bytes,
which is **471.9 Mb/s over 5 s**. On a 100M port that shows as 472 % "busiest" and the row is marked as trouble. No
sysUpTime is read in Traffic to detect a restart. A non-conforming agent that sends a value above 2^32 in the
32-bit path makes `uint.MaxValue - p` underflow (result 1.8e19).
Fix: read sysUpTime before and after, and return -2 (reset) when it went back. Accept a wrap only when
`p <= uint.MaxValue` and the implied rate is at most ifSpeed. Otherwise show "reset".

### C8. Medium - Path MTR verdict blames "real loss starting at hop N" for loss that is only at the destination
`PathTools.cs:100-107`
```csharp
bool carries = hops.Skip(i).Where(x => x.Sent > 0).All(x => ... >= l * 0.6);
if (l >= 3 && carries) return "Real loss starts at hop " + h.N + ...
...
return "Loss at the destination only (" ...   // unreachable when endLoss >= 3
```
For the last hop, `Skip(i)` holds only that hop, so `carries` is always true. Run under mono with hops 1-5 at 0 % and
the destination at 10 %: "Real loss starts at hop 6 (10.0.0.6) and carries on to the end - 10% lost from there." The
"destination only / rate-limits pings" sentence can only appear for a loss under 3 %.
Fix: run the loop to `hops.Count - 1`, and report the last hop as "destination only".

### C9. Medium - IPv6 vs IPv4: an IPv6-only site (or a broken IPv4) is reported as "Neither connected." in green
`PathTools.cs:197-200`. Run under mono with v4 = none and v6 = 20 ms: "Neither connected.", and the verdict level is
"0". When IPv4 fails and IPv6 works, the result is also "Neither connected.". When both fail it is level 0 (green).
Fix: enumerate all four combinations, and use level 2 for "neither".

### C10. Medium - Routing listen: DHCP traffic from any client is counted as "More than one DHCP server answers"
`RoutingTools.cs:160-161, 226`
```csharp
var from = kv.Value.Select(x => x[2] != "" ? x[2] : x[3] != "" ? x[3] : x[1]).Distinct().ToList();
case "DHCP": lv = sources > 1 ? 1 : 3; return sources > 1 ? "More than one DHCP server answers on this network ..."
```
The filter `udp port 67` captures client DISCOVER and REQUEST from 0.0.0.0 and broadcast INFORMs from client
addresses. One client renewing plus the real server already makes 2 sources, so an amber "rogue DHCP" warning appears
on almost every busy LAN. Fix: count distinct `dhcp.option.dhcp_server_id` (already captured as field 10) only on
OFFER and ACK (`dhcp.option.dhcp == 2 || 5`).

### C11. Medium - Gateway "takeover / failover" is logged whenever you move between networks that share a gateway IP
`RoutingTools.cs:35-46`
```csharp
static string lastGw = "", lastMac = "";
if (gw == lastGw && lastMac != "" && mac != lastMac) { LastFailover = ...; m.Events.Add(1, "gw", "The gateway " + gw + " moved to another device ...") }
```
Home 192.168.1.1, then the office, a café or a hotspot that also uses 192.168.1.1 gives an amber timeline event "a
router failover or a replaced router", and "Takeovers seen" in the Gateway view. Nothing is keyed by network (SSID,
adapter Id, DHCP server, Brain.NetKey). Fix: reset lastMac when the adapter, SSID or network key changes.

### C12. Medium - HealthCheck Wi-Fi "Your channel is calm · 0 other APs" on any non-English Windows
`HealthCheck.cs:76` (`Regex.Matches(outp, @"^\s*Channel\s*:\s*(\d+)", ...)`). `netsh wlan show networks mode=bssid` is
localized ("Kanal" in German, "Canal" in French, "チャネル" in Japanese, "Kanal" in Turkish), while "BSSID" stays as is.
The regex then finds nothing, so `same = max(0, -1) = 0` and the result is a green "calm" on a channel that may be
crowded. Fix: take the number from the line after each `BSSID n` line by its position, or use WlanGetNetworkBssList
(the Native WLAN API), which carries the channel frequency directly.

### C13. Medium - Paint loops spawn background work without end when the loader fails (CPU burn)
- `FullView.Travel.cs:52-59` and `186`. LoadApps catches the exception, posts `m365Rows = null` and invalidates.
  PaintApps then sees `m365Rows == null` and calls LoadApps again. If M365.FamilyByPid/Connections fails every time
  (WMI broken, access denied), process enumeration runs back to back for as long as the Teams box is open.
- `FullView.Insights.cs:18-21`. The same pattern with `History.Week`: if Week throws, weekLv stays null and 7 day files
  are re-read on every paint. Week **does** throw on a negative time-of-day row (see C16).
- `FullView.Travel.cs:133` (PaintRoutes) starts a new Routes.Read task on every paint until the first one returns.
Fix: set a "failed at" timestamp and back off. Write an empty list rather than null after a failure.

### C14. Low/Medium - "n/a, never 0" is broken in several places
- `Snmp.cs:374`: INTEGER that is empty or longer than 8 bytes gives Text "0" (fuzz-confirmed).
- `Snmp.cs:395` `Num()`: missing, empty, NULL (type 5) or longer than 9 bytes gives 0. Callers turn that into real
  values:
  - `SnmpGear.cs:270` `Gl()` only excludes End and OctetString. A NULL or empty value for upsEstimatedMinutesRemaining
    shows as a red "Runtime left 0 min" and "0% charged".
  - `SnmpGear.cs:88` gives FDB port "0", which is shown as "the switch itself".
  - `SnmpGear.cs:140` gives PVID "0".
  - `SnmpTools.cs:243`: a sensor with an empty value shows "0.00 °C".
- Num() ignores the sign: `SnmpTools.cs:242` `pr = (long)Snmp.Num(prec)`. entPhySensorPrecision is -8..9
  (RFC 3433), so precision -2 becomes 254 and the value is shown as `x / 10^254`, which is "0.00".
- `ReadPdu` (`Snmp.cs:158`): an empty or more-than-8-byte error-status reads as 0, so an error reply is taken as data.
- Counter64 with 9 significant bytes is silently truncated (fuzz). TimeTicks with 5 bytes is accepted.
Fix: make Num return `ulong?` (or -1), check the expected ASN.1 type per column, and refuse non-minimal or oversize
integers in ReadPdu.

### C15. Low/Medium - Replay shows "Reading that day..." forever after a load failure, and a corrupt history row breaks painting
- `FullView.Replay.cs:50`: if History.Load throws, `d == null`, so loading is cleared but nothing retries. Paint then
  shows "Reading that day..." for good (`data == null && Cfg.History`).
- `FullView.Replay.cs:106`: `replayDay.Value.Date.AddSeconds(sec)`. A history line starting with
  `10675199.02:48:05,...` parses (`TimeSpan.TryParse`, mono-confirmed) and AddSeconds throws
  ArgumentOutOfRangeException inside Paint on every repaint. That exception is only logged by Application.ThreadException.
- `History.cs:144` (used by Insights): `h = Math.Min(23, (int)(r[0] / 3600))` with a row such as `-01:00:00` gives
  h = -1, which throws IndexOutOfRangeException. Combined with C13, that becomes an endless retry.
- DST fall-back days: time-of-day seconds repeat for an hour. ReplayAt picks the first match, so the second 01:xx
  can't be reached by dragging.
Fix: in Load, drop rows with `t < 0 || t >= 1 day`. Set a load-error state in Replay.

### C16. Low - Overlap view: a full-tunnel VPN (0.0.0.0/1 + 128.0.0.0/1) is flagged "VPN route over your LAN - printers may become unreachable"
`PathTools.cs:178-179`. The prefix lengths are not compared. The connected /24 is more specific, so it still wins and
the LAN stays reachable. Two addresses in one subnet on the same NIC also give a red "X and X use the same
addresses". Fix: flag only when `r.Prefix >= l.Prefix` and the interfaces differ.

### C17. Low - RouteFor: "Windows has no route to it" is shown at level 0 (green)
`RoutingTools.cs:123`. `R(0, dst, rc == 0 ? ... : "Windows has no route to it", ...)`. It should be level 2.

### C18. Low - TimeSource shows a w32tm error as a green "Current source"
`SysTools.cs:298`. When the W32Time service is stopped, `w32tm /query /source` returns "The following error occurred:
The service has not been started. (0x80070426)". That string is non-empty and contains no "Local CMOS", so it gets
level 0. The `access is denied` branch computes advice "" in both arms (dead code).

### C19. Low - The Snapshot limit message is wrong, and Stop is slow
- `SnmpGear.cs:21` sets a 180 s deadline, but `Snmp.cs:351` says "stopped at the 2-minute limit".
- Stop is checked only between requests. One v3 Request can block for about 12 s (discovery 3 s, exchange 3 s,
  rediscovery 3 s, exchange 3 s), plus four 1 MB password-stretch hashes per request (`Localize` is called again for
  every message in both Message3 and Parse3). Cache the localized keys per engine.

### C20. Low - TeamsUdp reports the NAT as "keeps the same outside port for every server" after testing only one server
`WhyFails.cs:144, 167-170`. When DNS returns one relay, the same relay is queried twice, so `same` is always true.

### C21. Low - SNMP Interfaces delta: reboot detection depends on sysUpTime and ifIndex re-numbering is only partly detected
`SnmpTools.cs:99, 130, 136`. When sysUpTime can't be read, `upt = 0` and `reboot = false`. Counters that grew past the
old values after a restart then show as "+N". After a reboot that renumbers ifIndex (common on modular chassis and
Windows/Linux hosts) and without ifCounterDiscontinuityTime, deltas are calculated between *different* ports. The note
"a renamed port is still the same port" says the opposite. Fix: also compare ifDescr/ifName per index, and mark "reset"
when the name changed.

---------------------------------------------------------------------------------------------------------------------

## SUSPECTED (needs Windows or a real device to confirm)

### S1. Medium - "What's wrong?" fails the DNS step when port 53 to the configured server is blocked but Windows uses DoH, or when the first DNS server is dead and the second works
`Investigate.Whats.cs:98-109`. It sends one UDP/53 query to `DnsServer` (the first IPv4 DNS only, taken from the VPN
adapter when a VPN is up: `Monitor.cs:311-314`). Windows 11 DoH-only setups, a split-tunnel VPN DNS that refuses
public recursion, or a dead primary with a working secondary all lead to "names don't turn into addresses - so every
site and app fails". In each of these, name resolution works for every app. Fix: before concluding, cross-check with
`Dns.GetHostAddresses("www.microsoft.com")` (the OS resolver), and try every server.

### S2. Medium - IPv6-only networks and multiple active adapters feed wrong context into every Investigate flow
`Monitor.cs:293-305`. The gateway is IPv4-only, and "best" prefers any Ethernet adapter with a gateway over Wi-Fi,
whatever the route metric (for example USB phone tethering or a dock NIC on another network). On an IPv6-only / NAT64
network, Whats says "Windows has no default gateway ... Usually DHCP didn't finish" (`Investigate.Whats.cs:74`), and
the 1.1.1.1 tests fail. With two adapters, "your router" and its pings belong to an adapter Windows isn't using
(compare `GetBestRoute(0.0.0.0)`).

### S3. Medium - Stale or averaged ring data hides a link that is down right now
`Model.cs:47-53` `Ring.Avg` skips NaN. Whats uses `GwPing.Avg(6)` and `NetPing.Avg(6)`. If 5 of the last 6 pings
failed, it still reports the average of the one success and does not retest. The ring has no timestamps, so after
sleep/resume or with a slow `Cfg.NetSec`, the samples can be minutes old even though the guide says "only what's stale
is re-tested". Fix: keep sample times, and retest when the newest sample is NaN or older than 10 s.

### S4. Medium - MtuAt calls ICMP rate-limiting a "black hole"
`PathTools.cs:46-62`. Two silent probes at one size are taken as "too big". Many routers send only about 1 TTL-exceeded
per second. During the binary search that gives a lower MTU and `BlackHole = true`, and the Vpn() guide then concludes
level 2 "MTU black hole ... tell IT this number". Fix: confirm by re-probing a smaller size that worked and the failing
size after 1 s.

### S5. Low/Medium - WinEvidence.Auth counts *success* events as failed sign-ins
`WinEvidence.cs:93` queries WLAN-AutoConfig 12012 and Wired-AutoConfig 15501. As far as I recall these are "802.1X
authentication succeeded/restarted" events (failures are 12013 and 15502/15514). Each one is shown as a red row.
Check the IDs on a real system.

### S6. Low - Localized command output elsewhere (no wrong verdict, but the result degrades)
- `WhyFails.cs:55-57`: `netsh winhttp show proxy` "Direct access" and "Proxy Server(s)" are localized, so the row reads "unknown".
- `SysTools.cs:165-174`: gpresult regexes are English-only, so the result falls back to a raw 200-character dump.
- `SysTools.cs:133`: nltest labels are English in all locales (OK).
- `WinEvidence.cs` Reason regex: event text is localized, so it falls back to the first line (OK).
- No tracert, pathping, route print or arp output is parsed in this section: they use the ICMP and iphlpapi APIs.
- Number formatting is CurrentCulture for display only. No double.Parse of OS output was found in these files.

### S7. Low - Inspection decides "public CA" by name substring
`Inspection.cs:60-64`. A corporate or malicious root named "DigiCert Proxy CA", "Microsoft Intune Inspection" and so
on, installed in Root, counts as not inspected. Legitimate public roots that sit in Root but not AuthRoot and have no
listed name (TWCA, Certigna, SECOM...) count as inspected. Compare against the AuthRoot CTL (`certutil
-verifyCTL AuthRootWU`) by thumbprint, not by name.

### S8. Low - SNMP v2c community and v3 parsing oddities
`Snmp.cs:33-35, 114`. The community is trimmed. Non-ASCII characters become '?' (Encoding.ASCII), so the device stays
silent and the user is told "wrong community". A community that starts with "v3:" or "v3-xxx:" is taken as v3. An
ICMP port-unreachable on a connected UDP socket is reported as "no answer in 3 s" rather than "refused". One forged
packet with a wrong request-id, arriving first, aborts the request instead of being skipped (`Exchange` reads one
datagram). Replies from other IPs are correctly filtered by `Connect()`.

### S9. Low - SNMP CPU/memory view on Linux/Net-SNMP: "Physical memory" includes cache and buffers, so you get a red "Memory 98%"
`SnmpTools.cs:206`. hrStorageRam on Net-SNMP counts page cache as used. Subtract the "Memory buffers" and "Cached
memory" rows when they are present.

### S10. Low - Listening view ignores IPv6-only listeners
`SysTools.cs:240`. IPv6 sockets have `Proto == "TCP6"` (`Monitor.cs:885`), so a service bound only to `[::]` does not
appear among "ports open to the network".

---------------------------------------------------------------------------------------------------------------------

## Checked and OK
- Replies are accepted only from the target: the UDP socket is `Connect`ed, the request-id is matched, the community is
  compared for v2c, and v3 checks message-id, engine-id, user, HMAC (at the parsed offset) and the time window. Privacy
  can't be stripped by the device (`Snmp.cs:282`).
- The BER length decoder bounds every TLV against its parent (no OOB in 330k fuzz cases) and caps varbinds at 5000.
- Process.Start / Job.Run in these files only take system values (domain name, IPAddress, GUID, int seconds), so no
  user-controlled argument injection.
- Divide-by-zero guards exist for the Traffic dt, the Printer max, the PoE budget and storage size.
- Overall SNMP walk time is bounded by the per-run deadline; a fresh `Toolkit.Run` is made per run, so the deadline is
  not reused.
