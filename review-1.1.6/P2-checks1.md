# P2-checks1 - release check of 1.1.6 (999a100), slice src/Checks/Calc.cs .. OverTime.cs

Read every line of the 26 files. Harnesses (mono, r6.dll by reflection) in `scratchpad/review5/P2-checks1/`:
- `h.cs`: 132,000 calls through all 33 Calc* tools with hostile input (Unicode digits, 400-digit numbers, `1e400`, IPv4-mapped IPv6, octal and hex forms, URLs with user:pass, malformed decimals).
- `t.cs`: targeted parser checks (Calc, CertTools.Decode, DestCheck.Parse, Lab.Split, Lab.Addr, Games.ValidHost, Icmp.Resolve4).
- `f.cs`: 200,000 random inputs each through CertChain.Parse (TLS hello), Lab.DhcpText, EngPack.DecodeAttrs, MoreTools.ParseRootDse, CertChain.Oid2, CertTools.Csr and Json.Parse. Also a 3.9 MB JSON memory measurement and Lab.Addr on leading-zero addresses.
- `w.cs`: WhyFails.Hosts with `host` and with `host:port`, against a fake hosts file.

mono's IPAddress.TryParse matches .NET Framework's inet_addr reading here: leading zeros are octal, and short and hex forms are accepted. mono's double.Parse also throws OverflowException the same way .NET Framework 4.8 does.

### 1. Traffic Lab, RADIUS lab, Many ports, RDP and LDAP checks read leading-zero addresses as octal and send to a different host, sometimes a public one
Severity: security  
Where: src\Checks\Lab.cs line 106 (Addr: `IPAddress.TryParse(h)` first). Used by Lab.cs lines 136 (ICMP builder), 212 (UDP sender), 244 (TCP sender), 371 (SNMP trap), 397 (NetFlow), src\Checks\EngPack.cs line 270 (RadiusLab), and src\Checks\MoreTools.cs lines 176 (ManyPorts), 270 (Rdp) and 365 (Ldap). EngPack.RadiusLab and IspReport only gate with Games.ValidHost, which accepts these forms.  
How to reproduce: RADIUS client lab with `labtest@010.000.000.005`, a secret and a password. Or UDP sender with `192.168.001.010:5060`, or SNMP test trap to `10.0.0.010`.  
What happens / what should happen: `Lab.Addr("192.168.001.010")` returns 192.168.1.8, `"010.000.000.005"` returns **8.0.0.5** (a public address) and `"0x7f.1"` and `"2130706433"` return 127.0.0.1. So the RADIUS Access-Request goes to an internet host the user never named. That request carries the user name, the MD5-hidden password and an HMAC keyed with the shared secret, and so does a trap with its community string. Icmp.Resolve4 and DestCheck.Parse already refuse these forms ("isn't four plain numbers"). Lab.Addr, Games.ValidHost and Lab.Split should use the same rule. This is the round-4 #55 problem in places that fix didn't reach.  
Confidence: confirmed (ran it: t.cs/f.cs Lab.Addr results above; the send path was read)

### 2. Report for my ISP sends minutes of traces and MTR probes to a broadcast or multicast address
Severity: security  
Where: src\Checks\IspReport.cs line 57 (`Games.ValidHost(q)` is the only check), line 63 (`IPAddress.TryParse`), lines 74-77 (Trace, then Mtr for 1, 2 or 5 minutes, one probe a second per hop)  
How to reproduce: Report for my ISP with target `192.168.1.255`, `255.255.255.255` or `224.0.0.1`, and 5 minutes.  
What happens / what should happen: `Games.ValidHost("255.255.255.255")` is true, and Lab.NotOne is never called. The whole hop ladder is probed toward a broadcast or multicast group for up to 5 minutes, about 1 probe per second per hop. Every other sender now refuses these targets ("one device only"). Call Lab.NotOne after resolving, and refuse octal and short forms (#1).  
Confidence: suspected (read it; ValidHost results confirmed in t.cs)

### 3. Certificate and hash boxes refuse `\\server\share` but accept `//server/share`, which Windows opens as the same UNC path and signs in to
Severity: security  
Where: src\Checks\CertTools.cs line 32 (Input: `s.StartsWith(@"\\")` only), line 168 (Hash: same check). Input is used by Inspect, Chain check, PFX, CSR, Convert, Recipients, Match and Live. src\Checks\FileCrypt.cs lines 333-338 and 405-408 (Encrypt/Decrypt: no network-path check at all). Compare PsSafe at CertTools.cs line 375, which refuses both `\\` and `//`.  
How to reproduce: Inspect a certificate with `//attacker.example/share/cert.cer`, for example a path pasted from a message.  
What happens / what should happen: `Path.IsPathRooted` is true. .NET Framework normalises the separators, and `new FileInfo(...).Exists` opens `\\attacker.example\share\cert.cer` over SMB, sending the user's Windows sign-in (NTLM). The tool's own text promises the opposite: "Network paths (\\server\share) aren't read". The check should normalise the path first, or test `new Uri(path).IsUnc` / `FullName.StartsWith(@"\\")`. Encrypt/Decrypt should use the same check.  
Confidence: suspected (read it; can't open SMB from mono/Linux)

### 4. "A website or app won't open" never finds a hosts-file entry that pins the site
Severity: wrong result  
Where: src\Checks\Investigate.cs lines 137-138 (`hostOnly = d.Target`, which DestCheck sets to `host + ":" + port` at src\Checks\DestCheck.cs line 190) and line 142 (the "Your hosts file pins ..." verdict)  
How to reproduce: Put `10.9.9.9 example.com` in the hosts file, then run Investigate > A website or app won't open with `example.com`.  
What happens / what should happen: WhyFails.Hosts compares whole tokens, and `example.com:443` never equals `example.com`. The "Hosts file" observation and its red verdict never appear, so a stale pin (the classic "works on every PC but mine") is reported as a TLS or HTTP failure or as "network path works". Pass the bare host, for example `d.Target.Substring(0, d.Target.LastIndexOf(':'))`.  
Confidence: confirmed (ran it: w.cs, with a fake hosts file: `Hosts("example.com")` gives "Pins your site", `Hosts("example.com:443")` only "Entry")

### 5. [day 1] Roast my network grades a new install A+ from a single moment, and says to turn on History although it's already on
Severity: wrong result  
Where: src\Checks\Fun.cs lines 168-180 (Measure: with under 24 h of History it falls back to `m.Latest`; Level 0 = Smooth 99.9, `LossPct` 0 by default), line 185 (Grade), line 214 ("the last few minutes (turn on History for a week)")  
How to reproduce: First day after install (History on by default, Program.cs line 339). Toolbox > Roast my network, even in the first seconds while the snapshot is still "Waking up..." (Path 3,3,3,3,3).  
What happens / what should happen: The grade is "A+", with "Honestly? Hard to roast ... is doing okay" and a shareable PNG, all from one momentary verdict. A single amber second gives "C" and a red one gives "F" ("should be paying you"). The label tells the user to turn on History, which is already on. Until there is a day of History, either say there isn't enough data yet (as Letter does at line 251), or grade only from measured values. The hint should depend on Cfg.History.  
Confidence: suspected (read it; Snap defaults at src\Model.cs lines 138-141)

### 6. Calculators fail with "Couldn't finish - Input string was not in a correct format" (or "Value was either too large...") on Unicode digits, malformed decimals and very long numbers
Severity: wrong result  
Where (each one hit in the harness):
- Unicode digits pass `\d` and then hit `int.Parse(..., Invariant)` in src\Checks\Calc2.cs line 28 (Wildcard `٢٢`), line 54 (V6Split `2001:db8::/٤٨`), line 109 (Cellular `4x٤`, `٤ layers`), line 243 (Ups `٣ x 12 V 9 Ah`), lines 307/309 (Stream `1080p٦٠`) and line 367 (Plan `١٢٣ MB`). Same in src\Checks\CertTools.cs line 352 (Decode a JWT with `"exp":١٧٠٠٠٠٠٠٠٠`).
- `[0-9.,]+` and then `double.Parse` fails in Calc2.cs lines 174-175 (LinkBudget `1.2.3 km at 5 GHz`) and line 241 (Ups `. wh`, `1.2.3 kWh`).
- double.Parse overflows in src\Checks\Calc3.cs line 32 (Ps: AvgCost and Pnl fees with a 310+ digit number).  

How to reproduce: Run any of these with the inputs shown. Arabic and Bengali keyboards type these digits natively, and both languages ship in 1.1.6.  
What happens / what should happen: The Toolbox runner (FullView.Toolbox.cs line 902) catches the exception and shows a red "Couldn't finish" with the .NET message, untranslated. Each tool should show its own "type e.g. ..." row instead: use `[0-9]` (or RegexOptions.ECMAScript) and TryParse. This is the round-4 #60 class in places that fix didn't reach.  
Confidence: confirmed (ran it: h.cs and t.cs)

### 7. Calculators print -9,223,372,036,854,775,808 for very large values
Severity: cosmetic  
Where: src\Checks\Calc2.cs line 400 (Units: `(long)Math.Min(bytes * 8, long.MaxValue)`, where long.MaxValue as a double rounds up to 2^63); src\Checks\Calc.cs line 194 (Bandwidth "Full-size packets": `(long)pps` with a speed derived from size/time, which skips the 1 Pbps cap at line 174)  
How to reproduce: Units `9999999999999999999999 B`. Bandwidth size `999999 TB`, time `1 us`.  
What happens / what should happen: "... Gb · -9,223,372,036,854,775,808 bits" and "-9,223,372,036,854,775,808 a second". The derived speed should get the same 1 Pbps refusal, and conversions to long should be clamped below 9.2e18 (or the value printed as a double).  
Confidence: confirmed (ran it: t.cs)

### 8. Commodity converter reads "kilogram" as a gram, and "imperial gallon" as a US gallon
Severity: wrong result  
Where: src\Checks\Calc3.cs lines 152-155 (UnitOf: no alias for kilogram/kilograms/milligram/imperial; the longest-name substring search then finds "gram" in "kilogram")  
How to reproduce: Commodity units: amount 1, unit `kilogram` (or `2 kilograms`, `imperial gallon`).  
What happens / what should happen: "1.00 gram ... kg = 0.001" is off by 1000×, and the imperial gallon is 17% off (3.785 l instead of 4.546 l). Add the aliases (kilogram(s) → kg), and refuse words it doesn't know instead of matching a substring.  
Confidence: confirmed (ran it: t.cs)

### 9. Compare file vs live server and Certificate chain compare show the chain Windows rebuilt, not the one the server sent
Severity: wrong result  
Where: src\Checks\CertKeys.cs line 269 (Live: `foreach (var el in ch.ChainElements) sent.Add(...)`) and line 285 ("Intermediate" / "Root" rows under "The server presents"). src\Checks\EngPack.cs line 56 (CertCompare records `ch.ChainElements`) and line 78 ("Chain length differs ... One path adds (or leaves out) an intermediate")  
How to reproduce: A server that sends only its leaf (missing intermediate), with the intermediate in this PC's CA store or reachable by AIA. Run Compare file vs live server, or Certificate chain compare twice.  
What happens / what should happen: SslStream's chain comes from Windows' builder: intermediates from the local store or downloaded by AIA, and the root from the Root store. The tool lists a complete chain "presented" by the server, always ending in a "Root" row, so the missing-intermediate case it advises about can never show. Both runs on one PC usually get the same rebuilt chain, so the length comparison is meaningless. CertChain.ReadHello already reads the certificates the server really sends (TLS 1.2); these two tools should use it, or label the rows "as Windows built it".  
Confidence: suspected (read it)

### 10. The policy's online_lookups = off doesn't stop the public-IP lookup, DNS compare's default 1.1.1.1, or the sign-in page request that Clock treats as covered
Severity: security  
Where: src\Checks\IspReport.cs lines 177-180 and 368-381 (PublicIp: Cloudflare cdn-cgi/trace, no Policy.NoOnline check); src\Checks\MoreTools.cs line 124 (DnsCompare: empty box = 1.1.1.1, no policy check; DestCheck.CompareDns lines 527-539 switches to the user's own server under the same policy); src\Checks\DestCheck.cs line 108 and src\Checks\Investigate.Whats.cs line 30 (the msftconnecttest.com request checks only Cfg.Portal). src\Checks\WhyFails.cs lines 217-219 says the same request is switched off by "the policy's online_lookups".  
How to reproduce: AdeeboMeter.policy with `online_lookups = off`. Then run Report for my ISP with "yes" in the third box, or DNS answer compare with the second box empty, or What's wrong? on a network where the internet doesn't answer.  
What happens / what should happen: Cloudflare, 1.1.1.1 and msftconnecttest.com are each asked while the IT team has switched online lookups off, and other tools in the same files honour that switch. Check Policy.NoOnline in these three places and say "turned off by your IT team" the way VpnLeak does (OverTime.cs line 448).  
Confidence: suspected (read it)

### 11. One online-lookup answer can hold about 35-50 MB, and the cache keeps it until lookups switch off
Severity: slow  
Where: src\Checks\Online.cs lines 42-45 (reads up to 4,000,000 chars into a StringBuilder, then Json.Parse) and line 45 / line 19 (`cache[url] = o`, cleared only by Stop, up to 3 h later)  
How to reproduce: Online lookups on, then How the internet reaches me / Path owners. RIPEstat looking-glass and bgp-updates answers run to megabytes. Harness: Json.Parse of a 3.9 MB looking-glass-shaped document.  
What happens / what should happen: The parsed object graph measured about 27 MB, plus the 7.5 MB string and the StringBuilder's growth copies during the read. That breaks the 64 MB budget (rule 7) on top of the app's normal use, and every distinct URL stays cached. Cap the cache by size, keep only the few fields used, and lower the per-answer cap for the endpoints that are known to be large.  
Confidence: confirmed (ran it: f.cs measured the graph size; the real RIPEstat answer sizes are expected, not fetched)  
Rule: 7

### 12. Chain check says "nothing is fetched from the internet" while it fetches revocation lists
Severity: cosmetic  
Where: src\Checks\CertChain.cs line 203 (row text) and line 205 (`if (CertCfg.Revocation && !Policy.NoOnline) Revocation(...)`: X509RevocationMode.Online, which can also fetch AIA)  
How to reproduce: Settings > Certificate revocation checks on, then Toolbox > Chain check.  
What happens / what should happen: The text says nothing is fetched, and below it a "Revocation" row reports asking the issuer's list. When revocation is on, the text should say "plus the issuer's revocation list (Settings)".  
Confidence: suspected (read it)

### 13. About > What leaves your PC doesn't mention the 1.1.1.1 reference route in Report for my ISP
Severity: cosmetic  
Where: src\Checks\IspReport.cs line 58 (a typed host adds "Reference (1.1.1.1)", traced and MTR'd for the whole run); src\Ui\FullView.Settings.cs line 409 (LeaveIsp: "an MTR to the destinations you chose")  
How to reproduce: Report for my ISP with a host typed in.  
What happens / what should happen: 1.1.1.1 gets about one probe a second per hop for 1-5 minutes, and the About text doesn't mention it. Add "and to 1.1.1.1 for comparison".  
Confidence: suspected (read it)

### 14. The ISP report page mixes the user's language into an English, left-to-right page
Severity: cosmetic  
Where: src\Checks\IspReport.Visual.cs lines 41-47, 54-58, 116, 126-127, 139-141, 161 and 167-170 (chips and SVG labels via L.T); src\Checks\IspReport.cs line 329 (`<html lang="en">`, no dir) and lines 107-111 / 333 (Finding, table and message stay English)  
How to reproduce: Settings > Language: العربية, then Report for my ISP, and open the HTML.  
What happens / what should happen: Arabic chips and labels sit inside an English, left-to-right report meant for an ISP support desk, with English findings and table, and the support desk may not read Arabic. The report should be all one language, which for a provider is English (drop L.T in Pages). If it is translated, it needs lang/dir set.  
Confidence: suspected (read it)

### 15. After decrypting, the "It's a program" warning misses many file types that run when opened
Severity: security  
Where: src\Checks\FileCrypt.cs line 293 (RunsWhenOpened)  
How to reproduce: Decrypt a .adeebo whose name inside is `invoice.appref-ms`, `x.iso`, `x.jar`, `x.chm`, `x.msix`, `x.docm`, `x.psm1`, `x.msc` or `x.scf`.  
What happens / what should happen: The plain "Opened" row appears with no warning. The Zone mark is copied only if the .adeebo itself carried one. Extend the list: appref-ms, application, appx/msix(bundle), iso/img/vhd/vhdx, jar, chm, msc, scf, settingcontent-ms, library-ms, psm1, ps1xml, wsh, docm/xlsm/pptm, xll, gadget.  
Confidence: suspected (read it)

## Still present (from round 4)

| Round-4 # | What | Status in 999a100 |
|---|---|---|
| #55 (octal / hex / short IPv4) | fixed in Icmp.Resolve4, DestCheck.Parse, Calc.V4 | still present in Lab.Addr, Games.ValidHost and Lab.Split callers - see #1, #2 |
| #60 (Unicode digits throw) | fixed in ManyPorts, ICMP builder | still present in Calc2 / CertTools.Decode - see #6 |
| #12 (UNC paths read) | CertKeys now goes through CertTools.Input | `//server/share` bypass and FileCrypt - see #3 |

Fixed and verified by reading: #3 (Clock gated by Cfg.Portal / NoOnline), #11 (PFX walks with Ladder, no X509Chain), #14 (P7b content-type check), #17, #27, #28, #30 (LeaveIsp now lists PTR and the modem), #45, #47, #49, #50, #53, #54, #57, #59, #61, #62 (IDN SNI), #64, #66, #67, #68, #69, #84, #85, #91, #94, #95, #96, #103 (64 KB cap), #105 (portal 6 s deadline), #121, #122, #123.

## Checked and held up

- **Binary parsers** (f.cs, 200,000 random inputs each, 0 exceptions): CertChain.Parse (TLS records), Lab.DhcpText (119/121 pointer loops), EngPack.DecodeAttrs, MoreTools.ParseRootDse and LdapDone bounds, CertChain.Oid2, CertTools.Csr DER walker, and Json.Parse (100,000 nested `[` refused at depth 64).
- **DestCheck.Parse**: refuses `0x7f.1`, `2130706433`, `010.1.1.1` and `http://user:pass@0x7f.1/`. URLs with user:pass take the host only. IDN goes out as punycode. `[::ffff:10.0.0.1]:443` is handled. Ports 0 and over 65535 are refused.
- **Icmp.Resolve4** refuses leading zeros, hex and short forms, broadcast, multicast and IPv4-mapped broadcast (Lab.NotOne).
- **Network reads**:
  - ReadHello: 15 s wall clock, 128 KB, records of 18 KB or less, 10 certificates.
  - Portal: 4 KB, 6 s total.
  - HttpSmall: cap while reading, 20 s deadline, same-site redirects only.
  - IspReport modem page: 512 K chars, one same-host redirect.
  - PortalTest: 200 chars.
  - LDAP: 16 KB, 4 s.
  - RDP: 64 B.
  - DevId SSDP description: 64 KB, same IP, LAN only, no proxy.
  - Hash, ManyPorts, AllIps, MySites, SmtpPath, IspReport and the Lab senders honour Stop.
- **Lab gating**: ICMP/UDP/TCP/ARP/DHCP/trap/NetFlow/ManyPorts all go through Lab.Allowed (NoLab plus the company tick), and the senders apply NotOne. Caps hold: a 100 ms minimum gap and 1,000 packets.
- **Crypto**: FileCrypt checks the MAC before writing on seekable input, uses constant-time compares, a 100k-2M iteration bound, temp files tracked and swept, FreeName strips paths, device names and RTL marks, and the code alphabet is unbiased (248 = 8×31). EcKeys checks the point is on the curve, accepts its own key names only, caps backups at 64 KB, and clears the body and private key on every path.
- **Rule 4**: RadiusLab zeroes the secret, password and attribute list on every path, and the run history line holds no secret. Backup passphrases and codes are only "k" rows with copy actions. Trap communities aren't echoed.
- **Rule 1**: CertChain revocation, Clock, CompareDns, Online.* (gated by Online.On and the policy) and the modem/PTR lookups (in LeaveIsp) all map to a pressed tool or an About row. Exceptions are #10 and #13 above.
- **Verdicts**:
  - Guided.Lag never blames an unmeasured leg (gwUnknown).
  - What's wrong? re-levels a quiet router and says "and names" when the portal check is off.
  - The ISP report colours non-carrying loss grey.
  - DestCheck's amber final verdict.
  - CertChain's mTLS "handshake didn't finish".
  - Expired under a day reads in hours.
- **Calc**: VLSM /0 and /30-/32 maths, Number (64-bit binary, masks), Stack (IPv6 fragment header), and Bandwidth's "1.000" refusal hold.
