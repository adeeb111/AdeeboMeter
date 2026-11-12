# 07 — Crypto, keys, policy, --report, portable mode, Store package (dev-1.1.5)

Reviewer: adversarial review, section D. Code: /home/user/AdeeboMeter-dev (dev-1.1.5, read-only).
Harness: /tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review/harnessD/
(h1.cs, h2.cs and h3.cs load the **shipped test build** `run1/AdeeboMeter.exe` through `Assembly.LoadFrom` and call its internal methods with reflection; verify.py decrypts a .adeebo file on its own; outputs are in h1.out, h2.out and h3.out).
"Confirmed (ran)" means I ran it against the test build under mono. "Confirmed (read)" means the code path is clear, but it depends on a Windows API that mono doesn't have.

## What holds up (verified, no finding)

- **The KDF really is PBKDF2-HMAC-SHA256.** The managed fallback uses the 4-argument `Rfc2898DeriveBytes(..., HashAlgorithmName.SHA256)`, not the 3-argument SHA-1 constructor. The test build's `ManagedPbkdf2("ABCD", salt 00..0f, 1000, 64)` gave a result byte-for-byte equal to Python's `hashlib.pbkdf2_hmac('sha256', ...)` (`036541a4…3f10`). The native `BCryptDeriveKeyPBKDF2` P/Invoke signature is correct: HMAC flag 8, ULONGLONG iterations.
- **The format matches its notes.** verify.py decrypted a test-build .adeebo file using only the documented layout: `ADEEBO 01 01 | iter | salt16 | iv16 | AES-256-CBC(len2|name|data) | HMAC-SHA256(all before)`, with key bytes 0–31 for AES and 32–63 for the HMAC. Result: `mac ok True`, name `a.txt`, body matched.
- **Encrypt-then-MAC with separate keys.** The MAC covers the magic, version, iterations, salt, IV and all ciphertext. It is checked in constant time **before** decryption. I flipped every one of header bytes 0–43: all 44 were refused. Truncating, appending 16 bytes, an empty file and a header-only file were all refused. Changing byte 6 or 7 (the version) is refused with a clear message, so no downgrade was found. A mode-3 backup can't be opened as mode 1.
- **The iteration field from the file is bounded to 100k–2M.** `0x7fffffff` was refused in 0 ms. 2,000,000 takes about 15 s with the managed fallback (mono). On Windows the native path takes about a second.
- **`NewCode`.** Uses `RandomNumberGenerator`, with rejection sampling at `b < 248 = 8*31` so there is no bias. Over 20,000 codes all 31 symbols appeared, each between 15,321 and 15,622 times (expected 15,484), and there were no duplicates. 24 symbols give about 119 bits.
- **Fresh salt and IV per file.** There is a random 64-byte file key in mode 2 and a new ephemeral ECDH key per recipient. The CNG `Hash` KDF works out to SHA-384(label‖ephPub‖recipPub‖Z), then SHA-512(…‖"wrap") as a one-time pad. The recipient blocks are under the MAC.
- **No leftover temp file on a wrong code or a refused file.** h3 left 0 `.opening` files. `--report` JSON escaping holds for `"` `\` control characters, U+2028 and lone surrogates. The result is valid JSON. The `level` value is an int, so the unquoted append is safe.
- **Rule 4 for generated codes.** The "The code" and "Recovery passphrase" rows are marked `k`. They are filtered out of Copy, Ctrl+C, Save and CSV, and the whole result is dropped from `tbLast`.

---

### 1. Any AdeeboMeter.policy next to the exe silently overrides the IT team's %ProgramData% policy and switches everything back ON (breaks rule 6)
Severity: security (High)
Where: src/Policy.cs lines 34–41 (`Policy.Load`)
```csharp
foreach (var dir in new[] { Program.Dir, Path.Combine(...CommonApplicationData, "AdeeboMeter") })
{ ... Parse(System.IO.File.ReadAllLines(f)); File = f; Program.Log("policy: " + f); return; }
```
How to reproduce: IT puts `internet_check = off`, `speed_test = off`, `traffic_lab = off`, `live_capture = off` and `mode = simple` in `C:\ProgramData\AdeeboMeter\AdeeboMeter.policy`. The user, or any user-level script, creates an empty `AdeeboMeter.policy` in the exe folder. The installed exe lives in the user-writable `%LOCALAPPDATA%\Programs\AdeeboMeter\<ver>\` (Setup.Base), and a downloaded copy lives in Downloads.
Ran against the test build (h2.out):
```
A) admin policy only:     NoInternetCheck=True NoSpeed=True NoLab=True NoCapture=True Mode=simple
B) + empty exe-dir file:  NoInternetCheck=False NoSpeed=False NoLab=False NoCapture=False Mode=
```
What happens: the first file found wins and the loop returns, so the ProgramData policy is never read. Every switch the IT team turned off comes back on, and the Settings rows are no longer locked.
What should happen: read **both** files and OR them together (the strictest wins). Better still, only trust %ProgramData% (admin-writable). A file in the exe's own folder should only ever add restrictions.
Confidence: confirmed (ran against test build under mono).

### 2. The policy line `mode = pro` switches Pro mode ON for a user who chose Simple, and the user can't switch it back
Severity: wrong result / rule 6 (Medium)
Where: src/Policy.cs line 63 (`case "mode"`), line 72 (`Apply`). The comment on line 23 says "pro is also accepted - it doesn't add anything".
How to reproduce: the user chose Simple. Put an `AdeeboMeter.policy` containing `mode = pro` next to the exe.
Ran (h2.out): `C) exe-dir policy 'mode = pro' ... Mode=pro` / `user chose simple -> Cfg.Mode after Apply = pro`.
What happens: every Pro tab appears (Monitor, Security, Toolbox, Traffic Lab…), and Settings › Mode is locked with "Set by your IT team".
What should happen: accept only `mode = simple`, since a policy may only narrow.
Confidence: confirmed (ran against test build under mono).

### 3. The policy permanently overwrites the user's own settings (GameIds wiped, internet check off, history days, mode)
Severity: wrong result (Low)
Where: src/Policy.cs lines 68–74 (`Apply` writes into `Cfg`); src/AppCtx.cs lines 206–208 and every later `SaveSettings`
What happens: `Apply` sets `Cfg.GameIds = ""`, `Cfg.NetSec = 0`, `Cfg.HistoryDays` and `Cfg.Mode`, and the next save writes them to AdeeboMeter.settings. If IT removes the policy later, the user's game list and internet-check choice are gone for good. The same happens when a planted policy (see #1) is removed.
What should happen: enforce the policy at read time (as Monitor.cs:212 already does for `NoInternetCheck`) and never write it into Cfg.
Confidence: suspected (read).

### 4. Uninstall deletes your key backups and the P-384 key index, but leaves the private keys behind. After reinstalling, files encrypted to you can't be opened
Severity: security / data loss (High)
Where: src/Setup.cs line 192 (`Directory.Delete(%LOCALAPPDATA%\AdeeboMeter, true)`); src/Checks/EcKeys.cs line 27 (`keys-ec.txt` in DataDir); src/Checks/CertKeys.cs line 27 (`OutDir` = DataDir\certs, where both kinds of backup are written: FullView.Toolbox.cs:265 and CertKeys.cs:317)
How to reproduce: My keys › New key (P-384) › Back up private key. The backup goes to `%LOCALAPPDATA%\AdeeboMeter\certs\…-key-backup.adeebo`. Then uninstall from the tray or Settings › Apps, and install again.
What happens:
- Uninstall says "Removing your history and captures…" but it also deletes `certs\` (every .pfx and key-backup.adeebo, plus CSRs and public keys), `recipients\` and `keys-ec.txt`.
- The CNG keys `AdeeboMeter-xxxxxxxxxxxx` stay in the Microsoft Software KSP, and the RSA keys stay in CurrentUser\My. Private keys are left behind after uninstall.
- `EcKeys.Decrypt` only tries keys listed in `keys-ec.txt` (line 135). After reinstalling, every .adeebo sent to you fails with "this file wasn't made for any of your keys", even though the private key is still in Windows, and the backup that would restore it was deleted too.
- The Store build behaves the same way: MSIX removes the package's AppData on uninstall (see #10).
What should happen: save backups through a SaveFileDialog (Documents or a USB stick), never only into the app's data folder. On uninstall, ask whether to remove the keys, and delete the CNG and RSA keys if the user says yes. Find P-384 keys by enumerating CNG key names with the `AdeeboMeter-` prefix rather than relying only on the index file.
Confidence: confirmed (read). The Windows deletion path is plain code.

### 5. Portable mode isn't isolated: the first run takes over the installed copy's "Start with Windows", and the shared single-instance mutex hands off to the installed copy
Severity: wrong result (Medium)
Where: src/AppCtx.cs lines 189, 208, 230 (`if (first) { ApplyAutoStart(); ... }`, `k.SetValue("AdeeboMeter", "\"" + Application.ExecutablePath + "\"")`, with `autoStart = true` by default at line 40); src/Program.cs line 130 (`Local\AdeeboMeter.single`)
How to reproduce: have AdeeboMeter installed and set to start with Windows. Copy the test exe and an empty `AdeeboMeter.portable` into a folder and run it while the installed copy is not running.
What happens: there's no settings file next to the portable exe, so `first` is true, and `HKCU\...\Run\AdeeboMeter` is overwritten with the portable exe's path. From then on the test copy starts at login, and nothing starts once the test folder is deleted. If the installed copy *is* running, `AskRunningToOpen` opens the installed one instead, or the "stuck" dialog offers `KillOthers`.
Also: RSA keys (CurrentUser\My, "AdeeboMeter: " prefix) and P-384 CNG keys are shared by every copy. A portable copy lists the installed copy's RSA keys and can delete them.
What should happen: when `Program.Portable` is set, never touch the Run key, use a mutex name tied to the exe path, and say clearly that keys are per Windows account.
Confidence: confirmed (read).

### 6. P-384 keys plus portable or USB use: the index travels and the key doesn't. A listed key can't decrypt, and "also for me" encrypts to a key you don't have here
Severity: wrong result (Medium)
Where: src/Checks/FileCrypt.cs line 213 (`EcKeys.All().FirstOrDefault()` with no `CngKey.Exists` check); src/Checks/EcKeys.cs line 135 (skips silently); src/Ui/FullView.Toolbox.cs line 273 (Delete)
How to reproduce: in a portable copy on a USB stick, make a P-384 key on PC A, then run the same stick on PC B.
What happens: My keys on PC B lists the key and lets you copy its public key. Opening files for it says "this file wasn't made for any of your keys". Encrypt with "Also for me" adds this missing key as "you". Delete on PC B removes the index line, so back on PC A the real key is orphaned (see #4).
What should happen: mark keys whose CNG key is missing as "not on this PC", and skip them for "also me" with a warning.
Confidence: suspected (read; needs CNG).

### 7. More than 20 P-384 recipients (or 255 or more) produces a file nobody can open, including you
Severity: wrong result / data loss (Medium)
Where: src/Checks/EcKeys.cs line 106 `new List<byte>(Magic2) { (byte)recipients.Count }` vs line 129 `if (count < 1 || count > 20) throw ... "damaged header"`; FileCrypt.cs lines 207–213 don't limit or de-duplicate the names
How to reproduce: Encrypt a file with "For" set to 20 saved P-384 recipients, with Also for me = yes (the default). That makes 21 recipients.
What happens: the encryption reports success ("Encrypted … for …"). Every recipient, and you, get "damaged header" when opening it. With 256 recipients the count byte wraps to 0. Duplicate names ("Bob, Bob") are added twice.
What should happen: refuse more than 20 on the encrypt side (or make both limits the same), and de-duplicate by public key.
Confidence: confirmed (read). Mode-2 parsing can't run on mono because a TypeLoadException is thrown for ECDiffieHellmanCng when the method is JIT-compiled. The limits are exact constants.

### 8. Mixed P-384 recipients with "Also for me" and no P-384 key of your own: no warning, and you can't open your own file
Severity: wrong result (Low)
Where: src/Checks/FileCrypt.cs line 213 (the EC branch) vs line 230 (the RSA branch warns "you have no key of your own yet")
What happens: if you only have an RSA key and encrypt for P-384 recipients, `me == null` and the code silently adds nothing. The result doesn't say that you can't open the file. The RSA branch does show that warning.
Also: if a name has both `Bob.cer` and `Bob.adeebokey`, the P-384 key always wins (`EcRecipient` is checked first). Re-adding a recipient under the same name overwrites the old key without a warning (CertKeys.cs lines 274 and 284). That is a silent key change, which a "check the fingerprint once" model needs to flag.
Confidence: confirmed (read).

### 9. The decrypt tool's Code (or key-backup passphrase) is kept in the in-memory last-result cache, and the box isn't masked (breaks rule 4)
Severity: security (Low–Medium)
Where: src/Ui/FullView.Toolbox.cs line 198 `SecretInput` only matches "Shared secret", "Community" and "Password". The decrypt tool's label is "Code - for files shared with a code, or a key backup's passphrase" (line 124). Lines 661–669 then build `shown`/`key` with the code and store `tbLast[t.Id] = new PrevRun { Inputs = key, ..., Shown = shown }`, because the decrypt result has no `k` row.
What happens: `ResultText` filters "Code", so Copy and Save are clean. But the code or recovery passphrase stays in `tbLast`/`tbPrev` (`Inputs`, `Shown`) for the whole session, and it is typed into an unmasked box. Rule 4 says such secrets are never "kept in the last result".
What should happen: add `label.StartsWith("Code")` to `SecretInput`, or have Decrypt blank `shown[1]`. Optionally mask the box.
Also (Low, by design): the per-row "copy:" button for the code or passphrase goes through `Clipboard.SetText`. That puts the secret in Windows clipboard history (Win+V) and Cloud Clipboard sync. Set the `ExcludeClipboardContentFromMonitorProcessing` / `CanIncludeInClipboardHistory=0` formats.
Confidence: confirmed (read).

### 10. Store (MSIX) build: certs, backups and recipients go to virtualized AppData. "Show" points Explorer at a path that doesn't exist outside the package, and uninstall deletes them
Severity: wrong result / data loss (Medium, High together with #4)
Where: src/Program.cs line 62 `WriteDir => Packaged ? DataDir : Dir`; DataDir = `%LOCALAPPDATA%\AdeeboMeter`; the `show:` handler at FullView.Toolbox.cs line 346 runs `explorer.exe /select,"<path>"`; FullView.Settings.cs line 190 says "Saved in your AppData folder"
What happens: for packaged desktop apps, Windows redirects new writes under `%LOCALAPPDATA%` into `%LOCALAPPDATA%\Packages\AdeebKhan.AdeeboMeter_…\LocalCache\Local\AdeeboMeter`. explorer.exe runs outside the container and can't see the virtual path. So "Backup … copy it somewhere safe" opens Explorer without selecting the file, and the user can't find it. The package's LocalCache is removed when the app is uninstalled. PLAN-1.1.5.md line 15 already lists "test the startup task and AppData paths on a real install" as still to do.
What should happen: save backups, CSRs and public keys through a SaveFileDialog, or into Documents (KnownFolder, not virtualized). Turn on `desktop6:FileSystemWriteVirtualization` handling, or resolve the real path before passing it to Explorer.
Confidence: suspected (documented MSIX behaviour; not run on Windows).

### 11. Version is still 1.1.4: the Store package becomes 1.1.4.0, which is already submitted, and --report says "AdeeboMeter 1.1.4"
Severity: wrong result (Medium, blocks the Store update)
Where: src/Program.cs lines 66–67 `Version = "1.1.4"`, `FileVersion = "1.1.4.0"`; store/pack.ps1 lines 11–14 derive the MSIX version from it
Ran: the test build's `--report` writes `"app": "AdeeboMeter 1.1.4"`, and the harness read `Program.Version = 1.1.4`.
What happens: PLAN-1.1.5.md line 16 says "first Store submission is 1.1.4.0 (in review) - 1.1.5.0 is the first update". pack.ps1 would build `AdeeboMeter-1.1.4.0.msixbundle`, which Partner Center rejects because the version doesn't go up. The release job checks for `RELEASE-NOTES-1.1.5.md` against the tag, so a v1.1.5 release fails. The CI store step is `continue-on-error: true`, so this failure is silent.
Confidence: confirmed (ran against test build under mono; read pack.ps1).

### 12. --report exits 0 "ok", saying "the way in all work", while one of its own steps couldn't run
Severity: wrong result (Medium for RMM / Intune use)
Where: src/Report.cs line 30 `int lv = r.Lv < 0 ? 3 : Math.Min(3, r.Lv);` takes only the final `Conclude` level and ignores observed steps at level 3 ("couldn't tell")
Ran: `mono AdeeboMeter.exe --report existing.json --seconds 5` → exit 0, and the JSON contains:
```
"verdict": "ok", "finding": "Nothing is wrong right now - ... the internet, names and the way in all work."
{ "what": "6. The way in", "value": "Windows' connectivity test couldn't run (NameResolutionFailure) - web traffic may need a proxy here", "level": 3 }
{ "what": "1. This PC: processor", "value": "no answer", "level": 0 }
"measured_ms": { ... "dns": null }   while observed says "14 ms from 8.8.8.8"
```
What happens: on a real PC behind a proxy or captive portal, a script gets exit 0 "fine", and the finding contradicts the observed list. "no answer" also counts as level 0. Exit 3 is also used for bad arguments or an unwritable path (lines 20–21), so a script can't tell "couldn't measure" from "you called it wrong". An exception type outside IO, UnauthorizedAccess or InvalidOperation is rethrown (line 36), and the process then dies with the CLR crash code instead of 3.
Smaller points: `--report` doesn't load the user's settings, so a user who turned the internet check off still gets 1.1.1.1/8.8.8.8 pinged. An existing .json is overwritten without a check (confirmed: `existing.json` was replaced). That is acceptable for a CLI, but should be documented.
What should happen: use "couldn't tell" (3) when any step is level 3 and no step is 2. Use a distinct exit code (for example 4) for usage or I/O errors. Catch everything in Report.Run.
Confidence: confirmed (ran against test build under mono; the NameResolutionFailure comes from the sandbox, but the verdict logic is platform-independent).

### 13. A crafted key backup can inject lines into keys-ec.txt (name containing CR/LF or '|')
Severity: security (Low; the victim has to restore a backup file plus a passphrase the attacker supplied)
Where: src/Checks/EcKeys.cs lines 169–172 (`Restore` takes the name from the decrypted body with no `Safe()`); line 50 `Save` writes `"ec|" + k.Name + "|" + ...` with no escaping
Ran (h1.out): a backup body of `3|a\r\nec|evil|...` gets through parsing and reaches `Create(name, priv)` (on mono it then stops at CNG with NotImplemented). On Windows, with a valid private blob after the name, `Save` would write the injected `ec|…` lines, adding fake "My keys" entries such as a known public key under a trusted-looking name. A name containing `|` makes the line have more than 5 fields, and that restored key silently disappears from the index next time it is read.
Also: malformed bodies throw `ArgumentOutOfRangeException` ("nobar", "-5|x|", "99999|x|"), not CryptographicException. Toolkit's catch-all shows "Couldn't finish" and the log gets a stack trace. That's harmless, but the error is the wrong one.
What should happen: apply `Safe(name, 60)` in Restore, and reject `|`, CR and LF in `Save`.
Confidence: confirmed (ran) up to Create; the Windows Save step is suspected.

### 14. One malformed line in keys-ec.txt breaks My keys, opening P-384 files and "also for me"
Severity: crash-like / wrong result (Low)
Where: src/Checks/EcKeys.cs line 43 (`All()` checks the field count and date but not the base64); CertKeys.cs line 102, EcKeys.cs line 135, FileCrypt.cs line 213 then call `Convert.FromBase64String(k.Pub)`
Ran (h3.out): with a line `ec|Bad|K|!!notbase64|202601010000`, `CertKeys.MyKeys()` → `THROWS FormatException`. Opening a mode-2 file throws the same FormatException inside `FirstOrDefault`, which FileCrypt.Decrypt doesn't catch, so the user gets "Couldn't finish".
How this happens without an attacker: `Save` uses non-atomic `File.WriteAllLines`. A crash or power cut during a write leaves a cut-off base64 field.
What should happen: validate the base64 and the 104-byte length in `All()`, and write the file atomically (temp file, then replace), as the settings file already does.
Confidence: confirmed (ran against test build under mono).

### 15. Hostile mode-2 ephemeral key types throw ArgumentException, not CryptographicException
Severity: cosmetic / robustness (Low)
Where: src/Checks/EcKeys.cs line 97 `CngKey.Import(theirPub, EccPublicBlob)` → `DeriveKeyMaterial(other)`, with ephPub straight from the file (lines 133 and 138), not checked like `ReadPublic` checks its key (104 bytes, ECDH P-384)
What happens: an ECDSA P-384 blob (ECS3) or an ECDH P-256 blob in a recipient block whose key id matches yours makes .NET throw `ArgumentException` (ECDH requires an ECDH key, or the key sizes don't match). FileCrypt.Decrypt only catches CryptographicException, IOException and UnauthorizedAccess, so the user sees "Couldn't finish" plus a logged stack trace. Nothing is written, because the finally block deletes the temp file.
What should happen: validate `ephPub.Length == 104` and its magic before importing, and map the error to "damaged".
Confidence: suspected (read; CNG is not on mono).

### 16. MAC-then-reread double fetch: storage that changes between the two passes can feed unauthenticated ciphertext
Severity: security (Low)
Where: src/Checks/FileCrypt.cs lines 128–147 (`Open`): pass 1 computes the HMAC over the file, then `input.Position = ctStart` and pass 2 decrypts by **reading the file again**
What happens: a local writer is blocked by `FileShare.Read`. But a file on an SMB or WebDAV share, a cloud-sync placeholder, or a malicious USB device can serve different bytes on the second read. Those bytes are decrypted under the genuine key with no MAC on them (CBC bit-flipping), and the UI says "the file checked out". This undermines the "changed file is refused, never half-opened" claim against storage-level attackers.
What should happen: for files up to some size, decrypt from the bytes already MAC'd (a buffer). Otherwise copy to a local temp file first, or use chunked AEAD (STREAM construction) in a v2 format.
Confidence: suspected (read).

### 17. A half-decrypted file stays in the folder if AdeeboMeter is closed or killed mid-decrypt; Stop doesn't stop encrypt or decrypt
Severity: security (Low; rule "anything left in a temp file on failure")
Where: src/Checks/FileCrypt.cs line 250 (`"." + Guid + ".opening"` next to the encrypted file); line 290 (cleanup only in `finally`); Toolkit runs it on a LongRunning **background** task (FullView.Toolbox.cs line 666)
What happens: plaintext is only written after the MAC check, which is correct. But on a 1–4 GB file, closing the window, logging off or a crash ends the background thread without running `finally`. A partial plaintext `.<guid>.opening` is left in Downloads. On Windows a leading dot doesn't hide a file, and OneDrive may sync it. `run.Stop` is never checked inside `Seal`/`Open`, so the Stop button does nothing. Encrypt similarly leaves a cut-off `name.adeebo` under the real output name.
What should happen: check `run.Stop` in the copy loops, set `FileOptions.DeleteOnClose` and the hidden attribute on the temp file and clear them on success, and write encryption output to a temp name, then rename.
Confidence: suspected (read).

### 18. The decrypted file name is only sanitized for path escape; on Windows, extension spoofing (U+202E) and some device names get through
Severity: security (Low)
Where: src/Checks/FileCrypt.cs lines 168–180 (`FreeName`)
Ran (h1.out):
```
FreeName(..\..\Windows\evil.exe) -> evil.exe     FreeName(C:\x\evil.exe) -> evil.exe   FreeName(..) -> opened-file
FreeName(CON) -> opened-file-CON                  FreeName(report\u202efdp.exe) -> report\u202efdp.exe   (shows as "reportexe.pdf")
FreeName(CON .txt) -> CON .txt   FreeName(COM\u00b9.txt) -> COM\u00b9.txt   FreeName(CONIN$) -> CONIN$
```
`':'` (ADS), `*` and the like are removed on Windows, because .NET Framework's `GetInvalidFileNameChars` includes them (mono's list doesn't, so `a:b.txt` survives only on Linux).
What happens: anyone with the code, or anyone with your public key (see #19), picks the name. RLO (`char.IsControl` is false for Cf characters) lets `invoice‮fdp.exe` show up in Explorer as `invoiceexe.pdf` after "Show". `CON .txt`, `COM¹`, `CONIN$` and `CONOUT$` are Windows device aliases. File.Move then fails with an IOException, which is handled and the temp file deleted, so this is only a confusing error.
What should happen: strip Unicode category Cf (bidi overrides). Check the reserved list against the stem after `TrimEnd(' ')` and add COM¹²³, LPT¹²³, CONIN$ and CONOUT$. Consider warning when the decrypted file has an executable extension.
**The self-test failures under mono are a Linux artifact, not a sanitizer bug.** SelfTest.cs line 638 compares `Path.GetDirectoryName(fn)` (`/tmp`) with `Path.GetTempPath().TrimEnd('\\')` (`/tmp/`, because only `\` is trimmed). The resulting `fn` = `/tmp/evil.exe` is in fact correct. `FreeName` splits on both `\` and `/` itself, so path escape doesn't depend on the platform. Only the invalid-character set and device names do.
Confidence: confirmed (ran) for the name output; the Windows device behaviour is suspected.

### 19. "The file checked out" overstates what's checked: no sender authentication, and .p7m has no integrity at all
Severity: security / wrong result (Low–Medium, a UX claim)
Where: src/Checks/FileCrypt.cs line 259 ("…; the file checked out"), line 273 ("the file checked out (not changed since it was encrypted)"); the .p7m branch at lines 277–282
What happens: in mode 2, **anyone holding your public key** (which is meant to be shared) can make a .adeebo that opens with "the file checked out". Any one of several recipients can also re-encrypt the content for the others. In mode 1, anyone with the code can. Standard EnvelopedCms (.p7m, AES-CBC with no MAC or signature) is malleable, and a bit-flipped .p7m opens without any warning.
What should happen: word it as "not damaged in transit", not "from the sender". For .p7m, say it isn't tamper-checked, or sign it (SignedCms inside EnvelopedCms).
Confidence: confirmed (read).

### 20. The RSA PFX backup hint is wrong: "Toolbox › Check a PFX" doesn't put the key in My keys
Severity: wrong result (Low)
Where: src/Checks/CertKeys.cs line 323 ("double-click the .pfx (or Toolbox › Check a PFX), type the passphrase - then it's in My keys there too") vs CertTools.cs line 167 (`col.Import(..., EphemeralKeySet)`, which is never persisted)
Also: the PFX passphrase is the literal `XXXX-XXXX-…` with dashes and in upper case. Unlike .adeebo, where `Clean()` ignores case and dashes, the Windows import wizard needs it typed exactly. The wizard also makes the key non-exportable unless the user ticks the box, so "Back up private key" later fails with "this key was made not to leave the PC". .NET 4.8's `Export(Pfx)` uses legacy 3DES/SHA-1 PBE (2000 iterations). That is fine only because the passphrase is about 119 random bits.
Confidence: confirmed (read).

### 21. CMS (.p7m) encrypt and decrypt hold up to 200 MB several times over in memory (rule 7: 64 MB budget)
Severity: slow (Low)
Where: src/Checks/FileCrypt.cs line 235 (`File.ReadAllBytes(file)` → `ContentInfo` → `Encode()` → `WriteAllBytes`) and line 279 (`Decode(ReadAllBytes)` → `ContentInfo.Content`)
What happens: a 200 MB file means roughly 600–800 MB peak working set and LOH fragmentation. On a 4 GB PC or an ARM tablet an `OutOfMemoryException` is likely, and FileCrypt doesn't catch it, so the user gets "Couldn't finish".
What should happen: lower the limit (for example 50 MB) or point users to the streaming .adeebo modes.
Confidence: suspected (read).

### 22. Small wrong messages and leftovers in the encrypt and decrypt tools
Severity: cosmetic (Low)
- A code-mode .adeebo that was renamed (for example `file.bin`) goes to the CMS branch. Ran: `ASN1 corrupted data.` + "This file wasn't made for any of your keys". Detection should use the 8-byte magic (`ADEEBO 01 01`), not the extension (FileCrypt.cs line 267).
- A mode-2 MAC failure shows the code-mode advice "Check the code…" (line 287 uses the extension).
- A recipient named `code` can never be chosen (line 191).
- `EcAction("pub")` overwrites an existing `-public.adeebokey` (FullView.Toolbox.cs line 259). That's harmless.
- The EC backup thread (FullView.Toolbox.cs line 290) rethrows anything outside IO, UnauthorizedAccess and Crypto **on a raw background Thread**. An `ArgumentException` or `FormatException` from a malformed index line (see #14) would end the whole process (AppDomain.UnhandledException only logs).
- The name inside a .adeebo is cut to 255 bytes with `Take(255)`, which can split a UTF-8 character (U+FFFD in the opened name).
Confidence: confirmed (ran) for the first item; the rest read.

### 23. Store manifest and startup
Severity: Low / notes
- Capabilities: only `runFullTrust`, which is correct for a WinForms full-trust app. There are no internetClient or privateNetwork capabilities, and none are needed for full trust.
- `StartupTask Enabled="true"`: Windows keeps the switch. `StoreStartup.On` (AppCtx.cs lines 393–405) reads the undocumented `HKCU\...\SystemAppData\<PFN>\AdeeboMeter\State`. Before the user ever touches it, the value may be missing, so Settings shows "off" while the task is enabled. Use `Windows.ApplicationModel.StartupTask.GetAsync("AdeeboMeter").State` (WinRT), or treat "missing" as the manifest default. Suspected.
- In the Store build, `Program.Dir` is under WindowsApps and read-only, so the policy override in #1 isn't possible there. A ProgramData policy is still read. The portable marker can't be planted there either.
- The `--remove-folder`, Uninstall and Run-key paths are correctly skipped when `Packaged`. The tray "Uninstall AdeeboMeter..." item only appears when `Setup.RunningInstalled`. OK.
- CI: the Store bundle isn't covered by the build-provenance attestation (only `dist/*` is). That's a minor gap, and the `continue-on-error` noted in #11 hides failures.

### 24. Portable marker planted in Downloads, and data mixing
Severity: Low
Where: src/Program.cs lines 163, 170
What happens: a drive-by download can put `AdeeboMeter.portable` (or `AdeeboMeter.settings` / `AdeeboMeter.policy`) into Downloads next to a downloaded exe. With the marker, history, `keys-ec.txt`, `recipients\` and `certs\` (key backups) go to `Downloads\data\`, which is often synced by OneDrive. The backups are passphrase-protected, but the user doesn't expect them there. A planted `AdeeboMeter.settings` in the exe folder is read as the user's settings (it is WriteDir for non-packaged copies). That includes `gmon=` (game ids, which count as "consent" to ping game servers in the background), and it also suppresses the install offer (`Setup.ShouldOffer`). Policy and settings planted there can't do more than settings already allow, so rule 6 isn't broken by this vector alone, but rule 1 consent (gmon) can be pre-set.
What should happen: only honour the portable marker and the settings file next to the exe when the folder isn't a known download location, or ask once ("Keep data next to the exe?").
Confidence: suspected (read).

---

## Harness runs (all against the shipped test build via reflection, mono 6.8)
- h1.out: PBKDF2 = SHA-256 (matches Python); code alphabet uniform; all 44 header-byte flips refused; truncate, append, empty, version and iteration bounds refused; mode3≠mode1; malformed Restore bodies throw ArgumentOutOfRange; FreeName table.
- h2.out: policy override (#1), `mode=pro` (#2), oversized exe-dir policy falls through to ProgramData (OK); JSON `Q()` escaping valid.
- h3.out: wrong code leaves no temp file; renamed mode-1 goes to the CMS message (#22); a bad index line → MyKeys FormatException (#14); mode-2 paths blocked by TypeLoad (ECDiffieHellmanCng) on mono.
- verify.py: independent decrypt of a test-build .adeebo using only the documented format: MAC ok, plaintext correct.
- `--report` runs: `out.txt` → 3, `nodir/x.json` → 3, `d.json` (a directory) → 3, no argument → 3, `existing.json` silently overwritten → 0 with the contradictory verdict (#12).
