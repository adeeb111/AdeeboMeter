# Round 4 - S4: security checkup, certificates & HTTPS, crypto, capture replays, fun tools

Code: /home/user/AdeeboMeter-dev at e80412e. Files: SecChecks.cs, CertChain.cs (new), CertKeys.cs, CertTools.cs, EcKeys.cs, FileCrypt.cs, EngPack.cs, Pcap.cs, Replay.cs, Fun.cs (new).

Harness: scratchpad/review4/h4 (t.cs, t2.cs, t3.cs, fz.cs). It loads build4/r4.dll by reflection on mono, in portable mode. Test chains come from h4/gen.py (python cryptography): full, missing intermediate, wrong order, leaf expired 12 h ago, not yet valid, non-CA signer, cross-signed root, an 11-certificate chain, self-signed, RSA-1024 and EC P-384. They are served by local Python TLS servers (h4/srv.py, srv2.py): TLS 1.2 or 1.3, mTLS, leaf only, expired leaf. The test root is trusted in an isolated mono store (cert-sync --user).

How the harness differs from Windows:
- mono has no crypt32, so `CertChain.SignedBy` goes through a stub libcrypt32.so. It returns TRUE, or the value of STUB_SIG. This checks the walk and ordering logic. It does not check real signatures.
- mono's SslStream and X509Chain are not SChannel. Where that matters, the finding says so.

### 1. A hostile or oversized key-backup file is decrypted whole into memory before the HMAC is checked (300 MB file used 585 MB and took 12 s)
Severity: slow  
Where: src\Checks\EcKeys.cs line 185 (Restore: `FileCrypt.DecryptCode(input, o /* MemoryStream */ ...)`); src\Checks\FileCrypt.cs lines 129-157 (Open: one pass, decrypt and HMAC together), line 333 (only cap is 4 GB + 1 MB), line 350 (`File.OpenRead`, not the Stop-aware `In()`)  
How to reproduce: write `"ADEEBO" 01 03` + iterations 100000 (big-endian) + 32 random bytes + 300 MB of random data + 32 bytes. Open it with Toolbox › Open an encrypted file and any code. The harness h4/t2.exe calls FileCrypt.Decrypt on it.  
What happens / what should happen: The whole body is decrypted into a MemoryStream before the tag is compared. Result: "wrong code" after 12.2 s, with a peak working set of 585 MB. Files up to 4 GB pass the size check. The MemoryStream doubles its buffer up to the 2 GB limit, and the error is swallowed and reported as "wrong code". Stop does nothing on this path. A real key backup is under 1 KB. Refuse mode-3 files over a few KB, and check the tag before decrypting (it is a second pass over a tiny file). The format notes also don't match the code: FileCrypt.cs lines 19-20 say "checked BEFORE anything is decrypted ... never half-opened", and EcKeys.cs line 22 says "refused before anything is decrypted". Since round 1 the code decrypts and MACs in one pass: plaintext goes to a hidden temp file or to memory first, and is dropped if the tag fails.  
Confidence: confirmed (ran it: h4/t2.exe, 300 MB file, mono)  
Rule: 7

### 2. HTTPS check shows a green "A good padlock" when the handshake fails after the certificate (mTLS, client-certificate servers)
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 305-313 (hsErr is set but cert stays non-null) and lines 333-338 (the verdict never looks at hsErr once cert != null)  
How to reproduce: run a TLS server that requires a client certificate (h4/srv.py 8444 ... "tls12 mtls"), then run HTTPS check on localhost:8444. The same happens with any server that stalls or aborts after sending its certificate.  
What happens / what should happen: The verdict row is level 0, "A good padlock: , the right name, a chain Windows trusts." (the protocol is blank). The TLS version row underneath is red: "TLS 1.2 - the handshake stopped at the certificate". If the handshake didn't finish, the verdict should say so, for example "the server wants a client certificate / the handshake failed: <hsErr>", and not claim a good padlock.  
Confidence: confirmed (ran it: h4/t.exe https localhost:8444)

### 3. HTTPS check sends an HTTP request to msftconnecttest.com when a certificate is expired or not yet valid, ignores the "Sign-in page check" switch, and says "no HTTP request"
Severity: security  
Where: src\Checks\CertChain.cs line 329 (`if (expired || early) clock = WhyFails.Clock();`) and line 339 (the advice text "Two short handshakes, no HTTP request"); WhyFails.cs line 210 (HEAD http://www.msftconnecttest.com/connecttest.txt with no Cfg.Portal or Policy check); FullView.Toolbox.cs line 517 ("two short handshakes (no HTTP request)")  
How to reproduce: turn off Settings › Sign-in page check, then run HTTPS check against a site whose certificate is expired (h4 server 8446, or expired.badssl.com).  
What happens / what should happen: A "Clock" row appears ("couldn't compare: NameResolutionFailure" in the offline container), which proves the request to www.msftconnecttest.com was attempted. It also starts w32tm. Program.cs line 10 and the About row say this request is made "only if a check you started fails" and that "Settings can turn that off". Here the switch is ignored, and the tool text says no HTTP request is made. Gate the call on Cfg.Portal (and Policy.NoOnline), or only check the local clock, and correct the description. DestCheck.cs line 303 and Investigate.cs line 133 have the same ungated call.  
Confidence: confirmed (ran it: h4/t.exe https localhost:8446 shows the Clock row)  
Rule: 1

### 4. "Check a PFX" (and the inspection test) build the chain with X509Chain, which downloads missing intermediates from the internet, so "nothing sent" is false and a PFX without its intermediate reads as complete
Severity: security  
Where: src\Checks\CertTools.cs lines 254-258 (Pfx: `new X509Chain` ... `ch.Build(leaf)` with only RevocationMode.NoCheck); header comment line 16 "all on this PC, nothing sent"; CertChain.cs line 354 → Inspection.InspectedBy (the same kind of X509Chain)  
How to reproduce: on Windows, check a PFX that holds only the leaf and its key, issued by a public CA. The leaf has an AIA "CA Issuers" URL, and the intermediate is not cached on this PC.  
What happens / what should happen: .NET Framework 4.8 has no DisableCertificateDownloads option. CertGetCertificateChain fetches the intermediate over HTTP from the AIA URL (up to 15 s by default, possibly on the calling thread). The PC contacts the CA without the user asking (rule 1). ChainElements then includes the downloaded intermediate, so the row says "Chain: 3 levels up to a root" with no warning, even though the PFX is missing its intermediate and the server will send an incomplete chain. CertChain.cs's own header explains why it avoids Windows' chain builder for exactly this reason. Use the CertChain.Walk ladder for PFX files too, or build the chain with CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL through P/Invoke.  
Confidence: suspected (read it; Windows CryptoAPI behaviour)  
Rule: 1

### 5. Match / Live / Recipients read any path, including \\server\share UNC paths, and files of any size, which CertTools deliberately refuses
Severity: security  
Where: src\Checks\CertKeys.cs line 213 (Load: `File.Exists(s) ? File.ReadAllBytes(s)`), line 281 (Recipients: `File.Exists(ci) ? File.ReadAllText(ci)`, outside the try)  
How to reproduce: in "Check certificate and key", "Compare file vs live server" or "Recipients", type \\203.0.113.5\share\a.cer, or a path to a 1.5 GB file.  
What happens / what should happen: File.Exists opens an SMB session to the host, and Windows sends the user's NTLM credentials. CertTools.Input (line 32) and PsSafe (lines 370-371) refuse \\ paths for exactly this reason. A big file is read whole into memory. In Recipients, a locked or unreadable file throws IOException/UnauthorizedAccessException outside the try. Use CertTools.Input (rooted, no UNC, 1 MB cap) here too.  
Confidence: suspected (read it; Windows behaviour)  
Rule: 1

### 6. Chain check: a cross-signed copy of a trusted root is preferred over the trusted root itself, so a valid chain reads red as "the middle certificate is missing"
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 77-80 (Walk: candidates ordered by "signed it", then "in date", with files you gave first; trusted roots are never preferred)  
How to reproduce: Chain check with h4/c/chain-cross.pem (leaf, intermediate, and "Test Root CA" cross-signed by "Old Root"), with the self-signed Test Root CA trusted. In the wild: servers that send Sectigo's USERTrust cross-signed by AAA Certificate Services, or other cross-signs.  
What happens / what should happen: The plain-words row is red, "Your certificate is fine, but the middle certificate is missing - add it to the files above.", and the missing link is "Old Root". Windows and browsers stop at the trusted self-signed "Test Root CA" and accept the chain. When a candidate with the same name and key is a trusted root, prefer it (sort trusted roots first), or end the walk as soon as the current certificate's key matches a trusted root.  
Confidence: confirmed (ran it: h4/t.exe chain chain-cross.pem)

### 7. Chain check: a root that isn't in the local stores is reported as a missing *middle* certificate, including public roots Windows hasn't downloaded yet
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 65 and 81 (Walk only looks in Root/AuthRoot/CA as they sit on disk; any unknown issuer sets `missing`), lines 106 and 125 (the text says "middle certificate")  
How to reproduce: Chain check with h4/c/srv-chain.pem (leaf + intermediate) on a profile whose stores don't hold the root. On Windows this happens with any public chain whose root hasn't been fetched into AuthRoot yet: Windows fills AuthRoot on demand, as Inspection.cs line 74 itself notes. It also happens with a private CA whose root wasn't given.  
What happens / what should happen: The result is red, "Your certificate is fine, but the middle certificate is missing - add it to the files above", with "missing: the certificate that signed the one below (Test Root CA)". Over HTTPS it says "the server forgot to send the middle certificate", although servers aren't supposed to send roots. When the missing issuer is the issuer of a CA certificate, call it the root. Look it up in Windows' own list (Inspection.InWindowsList already reads the AuthRoot CTL) before calling it missing.  
Confidence: confirmed (ran it with empty stores; the AuthRoot on-demand part is suspected)

### 8. HTTPS check calls any site signed by a private root "Your company is inspecting HTTPS", including intranet servers and home-lab CAs
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 354-355 (any non-public root → "Your company is inspecting HTTPS (...)" / "(a company proxy)"); Inspection.cs lines 74-86 (a root in Root that isn't on the public lists counts as inspection)  
How to reproduce: on Windows, with the company's internal root (or your NAS/home-lab root) in Trusted Root Certification Authorities, run HTTPS check on intranet.corp.example or nas.local, which that CA signs directly.  
What happens / what should happen: An amber row says HTTPS is being inspected by a company proxy. Nothing is intercepting: the site legitimately uses a private CA. This inference only holds for a well-known public site (which is how SecChecks.TlsIntercept uses it, with www.microsoft.com). For an arbitrary site, say "signed by a private root this PC trusts (company or your own CA)". Only call it inspection when the site is publicly known to use a public CA, or when the same site looks different off the network (Certificate chain compare).  
Confidence: suspected (read it; needs a Windows store)

### 9. Startup check flags Windows' own programs as unsigned and counts every per-user Run entry twice
Severity: wrong result  
Where: src\Checks\SecChecks.cs lines 266-267 (`Tuple.Create(CU, RegistryView.Registry32)`) and line 276 (`X509Certificate.CreateFromSignedFile`)  
How to reproduce: run Security checkup › Programs that start with Windows on a stock Windows 10/11 64-bit with one per-user startup app (for example Discord).  
What happens / what should happen: CreateFromSignedFile only reads an embedded Authenticode signature. Catalog-signed Windows binaries, such as SecurityHealthSystray.exe in HKLM\...\Run ("SecurityHealth"), throw, so they are listed as "without a publisher's signature" (amber) on almost every PC. HKCU\Software is not redirected by WOW64, so the Registry32 view of HKCU opens the same Run key. Every per-user entry is then counted twice ("N start automatically" is inflated), and an unsigned one is named twice. Drop the HKCU Registry32 view. Check signatures with WinVerifyTrust (which covers catalogs), or at least skip files under %windir%.  
Confidence: suspected (read it; Windows registry and signing behaviour)

### 10. Windows version check gives "Pro Education" the Enterprise/Education support dates: 24H2 Pro Education shows "fixes until 2027-10-12", but its support ended on 2026-10-13
Severity: wrong result  
Where: src\Checks\SecChecks.cs line 151 (`Regex.IsMatch(edition, "Enterprise|Education")` matches EditionID "ProfessionalEducation")  
How to reproduce: VersionVerdict("26100", "ProfessionalEducation", "24H2", 2026-11-13).  
What happens / what should happen: The row is green: "Windows 11 24H2 (Enterprise / Education) · fixes until 2027-10-12". Microsoft's lifecycle puts Pro Education with Home/Pro, so the row should be red, "security fixes ENDED 2026-10-13", as it is for "Professional". Match "^(Enterprise|Education|IoTEnterprise)" on the EditionID, not a substring.  
Confidence: confirmed (ran it: h4/t.exe ver 26100 ProfessionalEducation …)

### 11. A CSR for a wildcard name gets the subject "CN=.example.com"
Severity: wrong result  
Where: src\Checks\CertKeys.cs lines 163 and 182-185 (Dn → Cn → Safe, which drops '*')  
How to reproduce: Toolbox › CSR with names `*.example.com` and an empty subject.  
What happens / what should happen: The CSR subject is "CN=.example.com" (Dn("*.example.com") returns that), while the SAN keeps *.example.com. CAs that require the CN to be one of the SANs reject it. Others issue a certificate with an invalid CN. Keep '*' in a leading "*." label for the CN. (The key's friendly name, "Local HTTPS - .example.com", loses it too.)  
Confidence: confirmed (ran it: h4/t.exe dn "*.example.com")

### 12. Roast my network: the picture includes the provider's name although the tool says "no provider name"
Severity: wrong result  
Where: src\Checks\Fun.cs lines 201-202 (burns use `prov` = m.Isp) and line 230 (the burns are drawn on the PNG); line 238 ("No Wi-Fi name, no addresses, no provider name - just the grade and the burns.")  
How to reproduce: Roast with a grade of A/A+ or F (Burns(week, "A+", "Acme Broadband Ltd")).  
What happens / what should happen: The burns are "Honestly? Hard to roast. Acme Broadband Ltd is doing okay. For now." and "Acme Broadband Ltd should be paying you." They go into the picture meant for posting, and the provider name usually narrows down where the user lives. Either use "your provider" in the picture or drop the promise.  
Confidence: confirmed (ran it: h4/t.exe burns)

### 13. Security checkup "This Wi-Fi's security" reads only the English netsh label, so open or WEP networks aren't flagged on non-English Windows
Severity: wrong result  
Where: src\Checks\SecChecks.cs lines 40-43 (WifiSec: `Authentication\s*:` on `netsh wlan show interfaces`)  
How to reproduce: on French or German Windows, join an open hotspot and run the checkup. netsh prints "Authentification : Ouvrir" / "Authentifizierung : Offen".  
What happens / what should happen: The regex doesn't match, so the row reads "not on Wi-Fi, or Windows doesn't show it" (info) instead of the red open-network warning. The Open/WEP words are English too. Read the auth algorithm from WlanApi (WLAN_CONNECTION_ATTRIBUTES.wlanSecurityAttributes), which the app already calls for Wi-Fi watch. (Round 1 #68 fixed this for saved networks only.)  
Confidence: suspected (read it)

### 14. HTTPS check sends an IDN host as "b?cher.example" in its TLS 1.2 hello (SNI)
Severity: wrong result  
Where: src\Checks\CertChain.cs line 271 (`Encoding.ASCII.GetBytes(sni)`); also line 309 (AuthenticateAsClient with the Unicode name)  
How to reproduce: CertChain.ClientHello("bücher.example", 0x0303). The server_name bytes are 62 3F 63 68 65 72 2E ... ("b?cher.example").  
What happens / what should happen: The server receives an invalid SNI. It answers with its default certificate or an alert, so the ladder and "Sent by the server" describe the wrong certificate, or the check fails. Windows' own handshake may also report a name mismatch against the punycode SAN. Convert with IdnMapping.GetAscii before building the hello and before AuthenticateAsClient.  
Confidence: confirmed (ran it: h4/t.exe hello)

### 15. Certificate expiry checks: a certificate that expired less than a day ago reads "0 days left" / "expired 1 days ago"
Severity: cosmetic  
Where: src\Checks\SecChecks.cs line 215 (CertExpiry: `(int)(NotAfter - Now).TotalDays` truncates -0.5 to 0, so `d < 0` is false); CertChain.cs line 353 ("expired " + Math.Max(1, -days) + " days ago")  
How to reproduce: Security checkup › Certificate expiring for a site whose certificate expired 12 hours ago. Separately, HTTPS check against h4 server 8446 (leaf expired 12 h ago).  
What happens / what should happen: The checkup says "0 days left (2026-11-13)" in red, never "EXPIRED". The HTTPS check says "expired 1 days ago". Compare against NotAfter directly (`DateTime.Now > NotAfter`), as CertChain does for its verdict.  
Confidence: confirmed for CertChain (ran it); suspected for SecChecks (read it)

### 16. Chain check stops after 9 certificates and then shows a green "In plain words" that says the root isn't trusted
Severity: wrong result  
Where: src\Checks\CertChain.cs line 67 (`depth < 8`), line 117 (level 0 when there's no Bad and no `missing`), line 130  
How to reproduce: Chain check with h4/c/chain-long.pem (leaf + 10 intermediates, root trusted).  
What happens / what should happen: The ladder ends at "Mid 2" with no root and no "missing" row. "In plain words" is level 0 (green) with the text "Every link holds, but the top is a root this PC doesn't trust". When the walk stops at the depth limit, say "chain longer than 9 - stopped" and give it at least amber.  
Confidence: confirmed (ran it)

### 17. Chain check treats the first certificate in a .p7b as "your certificate", but bundles are often stored root-first
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 139-147 (`certs[0]` is the leaf, `certs.Skip(1)` the helpers); CertTools.cs line 111 (P7b order as decoded)  
How to reproduce: Chain check on a .p7b whose SignedData lists root, intermediate, leaf (python cryptography's pkcs7.serialize_certificates writes them in that order: h4/c/chain-py.p7b; many CA "bundle" downloads do the same).  
What happens / what should happen: The ladder starts at the root and reads "your certificate: Test Root CA". For a bundle, pick the certificate that issues none of the others as the leaf. (mono can't import .p7b, so this was not run end to end.)  
Confidence: suspected (read it; order confirmed with openssl pkcs7 -print_certs)

### 18. The chain ladder doesn't check signature hash, key size, key usage or path length, so an RSA-1024 leaf passes as "Every link holds"
Severity: wrong result  
Where: src\Checks\CertChain.cs lines 42-46 (IsCa checks BasicConstraints only) and lines 61-97 (Walk); header comment lines 20-21 ("is the signer allowed to sign")  
How to reproduce: Chain check with h4/c/leaf-1024.pem plus int.pem.  
What happens / what should happen: Every row is green. Current clients reject RSA keys under 2048 bits, SHA-1 signed server certificates, missing serverAuth EKU, and pathLen violations. CertTools.Describe already flags RSA < 2048 and SHA-1 per certificate. The ladder, which is the part that gives the verdict, should do the same.  
Confidence: confirmed (ran it for RSA-1024; SHA-1, EKU and pathLen suspected)

### 19. PKCS7 path: a crafted "-----BEGIN PKCS7-----" block holding a passwordless PFX imports a private key
Severity: security  
Where: src\Checks\CertTools.cs lines 111, 130 and 152-158 (P7b: `col.Import(b)` without checking the content type; the comment says "Such a bundle holds no private key, so nothing reaches a key store")  
How to reproduce: base64 a PFX that has an empty password, wrap it in -----BEGIN PKCS7----- / -----END PKCS7-----, and give it to Inspect a certificate or Chain check.  
What happens / what should happen: On Windows, X509Certificate2Collection.Import with default flags writes the key into the user's key container. The certificate is then dropped because HasPrivateKey is true, and the key is removed when the object is disposed or finalized. A crash or kill in between leaves it in %APPDATA%\Microsoft\Crypto. Round 1 #17 was fixed for the CERTIFICATE path only. Check `GetCertContentType(b) == Pkcs7` before Import, as lines 114-115 already do for the raw-file case.  
Confidence: suspected (read it; Windows import behaviour)

### 20. Revocation: "couldn't tell - the issuer's list didn't answer in 10 s" also appears when the certificate has no CRL or OCSP address at all
Severity: cosmetic  
Where: src\Checks\CertChain.cs line 164  
How to reproduce: turn on the revocation setting and run Chain check on a private-CA certificate without a CRL Distribution Point or AIA OCSP.  
What happens / what should happen: Windows returns RevocationStatusUnknown at once, but the text blames a 10 s timeout. Say "no revocation address in the certificate" when there is no CDP or OCSP extension, and keep the timeout text for OfflineRevocation.  
Confidence: suspected (read it)

### 21. Letter to my provider says "The outages above ..." when there were no outages
Severity: cosmetic  
Where: src\Checks\Fun.cs line 263  
How to reproduce: History with router delay over 15 ms on average and no outages in the period; run Letter to my provider.  
What happens / what should happen: The letter says "The outages above are the internet check getting no answer at all, which my Wi-Fi alone doesn't explain." But no outages were mentioned, and with no outages the trouble may well be the Wi-Fi alone. The letter's own rule is "never lies". Add this sentence only when w.Outages > 0.  
Confidence: suspected (read it)

## Still present from earlier rounds

| Earlier id | Where now | One-line evidence |
|---|---|---|
| R1 #172 / R2 #92 (pcap memory), partly | Pcap.cs lines 37, 59-64 | The Wanted filter helps with web traffic, but a 60 MB DHCP-heavy capture still keeps 147k packets: 148 MB managed, 172 MB peak (h4/t3.exe big.pcap). |
| R1 #179 / R2 #93 (.p7m in memory) | FileCrypt.cs lines 28, 317, 365-369 | Still ReadAllBytes plus EnvelopedCms decode and copies, up to 100 MB in and 50 MB out; only the caps changed. |
| R1 S6 / 10-carryover S6 (update date culture) | SecChecks.cs line 133 | Still tries InvariantCulture (M/d/yyyy) first for InstalledOn; unchanged, now with a comment saying it's by design. |

## Checked and held up

- Pcap/pcapng fuzz: 200,000 mutated files (byte flips, magic values, 0/0x7FFFFFFF/0xFFFFFFFF lengths, truncation). Seeds were all 9 sample captures as classic pcap, plus a pcapng conversion with IDB if_tsresol=9, EPB and SPB blocks. Each went through Pcap.Parse, DhcpRows, RaRows, EapRows and IcmpRowsFromPackets: 0 exceptions, no case over 1 s (22 s total). The pcapng conversion parses to the same packet counts as the originals.
- CertChain Walk: wrong order, extra certificates, a non-CA signer (red), not-yet-valid and expired leaves (with the clock hint), an expired intermediate given next to the renewed one (the valid one is picked), self-signed, loop guard. HTTPS check against a TLS 1.2 full chain, leaf only, expired leaf, a TLS 1.3 server, a closed port, and a URL with a path: all reported correctly. Raw hello/Certificate parsing is bounds-checked (18 KB records, 128 KB, 10 certificates).
- Crypto: NewCode is unbiased (248 = 8 × 31, 118.9 bits). Iterations are clamped to 100k-2M. EcKeys KDF = SHA-384(label|eph|recip|Z), pad = SHA-512(secret|"wrap"); encrypt and decrypt match each other and the notes (apart from the "wrap" suffix, which isn't documented). The ephemeral key header is validated (ECK3/48). 1-20 recipients are enforced on both sides. The HMAC covers the header and is compared in constant time. A padding error is folded into the MAC failure (no oracle). FreeName strips paths, bidi and format characters and device names. Temp .part files are tracked and swept.
- Secrets: the RADIUS secret and password are wiped on every path and never written to aaa-runs.txt. Codes and passphrases come back in "k" rows, which the Toolbox keeps out of the last-result cache (IsSecretRow).
- Fun.cs: Therapy, Roast and Letter only read History/timeline and write to data\reports. No network calls. Points go through the normal once-per-tool path. The heist game's claims match hof_verify.py: HMAC-SHA256 over "v1|name|total|date", 16 base-32 characters = 80 bits (1.2e24 codes, about 38 million years at 1e9 guesses a second), MAX_POINTS 900, names cleaned, duplicates refused.
- SecChecks: the company/policy gates for DHCP and router probes are in place. LM interception roots are flagged. The Antivirus productState decode is correct. The About row now lists cloudflare-dns.com and www.microsoft.com. NewDevices locks the same object as the writer.
- EngPack: the CertCompare verdict names the subject (R1 #238 fixed) and keeps host:port. HowFar no longer shows green for a failed certificate. 802.1X judges every frame.
