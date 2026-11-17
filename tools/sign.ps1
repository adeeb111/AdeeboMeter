# Signs AdeeboMeter with Adeeb's Certum code signing certificate (Open Source Code Signing in the cloud, SimplySign).
#
#   powershell -ExecutionPolicy Bypass -File tools\sign.ps1                       signs .\AdeeboMeter.exe
#   powershell -ExecutionPolicy Bypass -File tools\sign.ps1 dist\x\AdeeboMeter.exe   signs that file
#
# Before running: SimplySign Desktop is open and logged in (the code from the SimplySign phone app). The private key never
# leaves Certum - Windows reaches it through SimplySign like a smart card, and this script picks the certificate by its
# thumbprint. No password, no key file, nothing to keep secret here.
#
# Every signature is timestamped (Certum's time server), so signed files stay valid after the certificate ends (17 Nov 2027).
# Signing changes the file, so its .sha256 fingerprint is written again.
param([string[]]$Files = @("AdeeboMeter.exe"))
$ErrorActionPreference = "Stop"
$Thumb = "28E4CAFF833777959D0FDFDEC450006CD6CA94F2"   # Open Source Developer MdAhasanul Adeeb Khan, Certum, to 2027-11-17
$Tsa = "http://time.certum.pl"

# signtool: the newest Windows SDK's x64 copy
$st = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $st) { Write-Host "signtool wasn't found. Install the Windows SDK with only 'Windows SDK Signing Tools for Desktop Apps' ticked."; exit 2 }

# the certificate, through SimplySign
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $Thumb
if (-not $cert -or -not $cert.HasPrivateKey) { Write-Host "The signing certificate isn't connected. Open SimplySign Desktop, log in with the code from the phone app, then run this again."; exit 3 }
if ($cert.NotAfter -lt (Get-Date)) { Write-Host "The certificate ended on $($cert.NotAfter) - renew it at Certum first."; exit 4 }

$bad = 0
foreach ($f in $Files) {
    if (-not (Test-Path $f)) { Write-Host "Not found: $f"; $bad++; continue }
    $p = (Resolve-Path $f).Path
    Write-Host "Signing $p ..."
    $ok = $false
    for ($try = 1; $try -le 3 -and -not $ok; $try++) {   # the time server is sometimes slow - try it up to 3 times
        & $st.FullName sign /sha1 $Thumb /fd sha256 /tr $Tsa /td sha256 /d "AdeeboMeter" /du "https://adeebometer.com" $p
        $ok = $LASTEXITCODE -eq 0
        if (-not $ok -and $try -lt 3) { Write-Host "  didn't work (try $try of 3) - trying again in 5 seconds"; Start-Sleep 5 }
    }
    if (-not $ok) { Write-Host "  NOT signed: $p"; $bad++; continue }
    & $st.FullName verify /pa /q $p
    if ($LASTEXITCODE -ne 0) { Write-Host "  signed, but Windows doesn't accept the signature: $p"; $bad++; continue }
    $s = Get-AuthenticodeSignature $p
    Write-Host "  OK - $($s.SignerCertificate.Subject.Split(',')[0]) - timestamped $($s.TimeStamperCertificate -ne $null)"
    # the fingerprint changes with the signature
    $h = (Get-FileHash $p -Algorithm SHA256).Hash.ToLower()
    if (Test-Path "$p.sha256") { $h | Set-Content -Encoding ASCII "$p.sha256" }
    Write-Host "  SHA256 $h"
}
if ($bad) { Write-Host "$bad file(s) not signed."; exit 1 }
Write-Host "All signed."
