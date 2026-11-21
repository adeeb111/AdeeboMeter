# Signs a published GitHub release in one go (run on Adeeb's PC, after GitHub has attached the files).
#
#   powershell -ExecutionPolicy Bypass -File tools\finish-release.ps1            the version in src\Program.cs
#   powershell -ExecutionPolicy Bypass -File tools\finish-release.ps1 1.1.6      a given version
#
# Before running: SimplySign Desktop logged in (code from the phone app), and the GitHub CLI logged in once
# ("gh auth login" - it keeps its own sign-in; nothing is stored by this script).
#
#   GitHub's files ──► sign AdeeboMeter.exe (tools\sign.ps1) ──► win64 zip rebuilt around it ──► new SHA256SUMS
#   ──► release notes: signed fingerprints + the exe exactly as GitHub built it (what build provenance covers)
#   ──► uploaded over the release's files ──► downloaded again and every fingerprint checked
#
# The source zip is GitHub's, unchanged. Nothing else on the release is touched.
param([string]$Version = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root
if (-not $Version) {
    $m = [regex]::Match((Get-Content -Raw "src\Program.cs"), 'public const string Version = "([^"]+)"')
    if (-not $m.Success) { throw "Version not found in src\Program.cs - give it: finish-release.ps1 1.1.6" }
    $Version = $m.Groups[1].Value
}
$tag = "v$Version"; $win = "AdeeboMeter-$Version-win64.zip"; $src = "AdeeboMeter-$Version.zip"
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Host "The GitHub CLI isn't installed: winget install GitHub.cli, then gh auth login."; exit 2 }
gh auth status *> $null; if ($LASTEXITCODE -ne 0) { Write-Host "The GitHub CLI isn't logged in: run gh auth login (once)."; exit 2 }
function Sha([string]$p) { (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant() }

$work = Join-Path $env:TEMP "adeebometer-release-$Version"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force $work | Out-Null
Write-Host "1/6 Downloading $tag's files ..."
gh release download $tag -R adeeb111/AdeeboMeter -D $work -p "AdeeboMeter.exe" -p $win -p $src -p "SHA256SUMS"
if ($LASTEXITCODE -ne 0) { throw "couldn't download $tag - is it published, and are its files attached yet?" }
# the files must be GitHub's own (their SHA256SUMS), not a half-finished earlier run
foreach ($l in Get-Content (Join-Path $work "SHA256SUMS")) { $h, $n = $l -split "  ", 2; if ($n -and (Sha (Join-Path $work $n)) -ne $h) { throw "$n doesn't match the release's SHA256SUMS - stop" } }
$exe = Join-Path $work "AdeeboMeter.exe"
if ((Get-AuthenticodeSignature $exe).Status -eq "Valid") { Write-Host "$tag's exe is already signed - nothing to do."; exit 0 }
$built = Sha $exe

Write-Host "2/6 Signing ..."
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\sign.ps1") $exe
if ($LASTEXITCODE -ne 0) { throw "not signed - nothing was uploaded" }
$signed = Sha $exe

Write-Host "3/6 Rebuilding $win around the signed exe ..."
$un = Join-Path $work "unzip"; Expand-Archive (Join-Path $work $win) $un
Copy-Item $exe (Join-Path $un "AdeeboMeter\AdeeboMeter.exe") -Force
[IO.File]::WriteAllText((Join-Path $un "AdeeboMeter\SHA256SUMS"), "$signed  AdeeboMeter.exe`n")
$out = Join-Path $work "out"; New-Item -ItemType Directory -Force $out | Out-Null
Compress-Archive -Path (Join-Path $un "AdeeboMeter") -DestinationPath (Join-Path $out $win) -CompressionLevel Optimal
Copy-Item $exe $out
$winSum = Sha (Join-Path $out $win); $srcSum = Sha (Join-Path $work $src)
[IO.File]::WriteAllText((Join-Path $out "SHA256SUMS"), "$signed  AdeeboMeter.exe`n$winSum  $win`n$srcSum  $src`n")

Write-Host "4/6 Release notes ..."
$body = gh release view $tag -R adeeb111/AdeeboMeter --json body --jq .body | Out-String
$oldWin = (Get-Content (Join-Path $work "SHA256SUMS") | Where-Object { $_ -like "*  $win" }) -replace "  .*", ""
if ($body.IndexOf($built) -lt 0) { throw "the notes don't carry GitHub's exe fingerprint - edit them by hand" }
$body = $body.Replace($built, $signed).Replace($oldWin, $winSum)
$body = $body.Replace("from this exact source.", "from this exact source, then code-signed by **Open Source Developer MdAhasanul Adeeb Khan** (Certum, timestamped). Signing adds only the signature: the exe exactly as GitHub built it - the file GitHub's build provenance covers - had SHA256 ``$built``.")
$notes = Join-Path $out "notes.md"; [IO.File]::WriteAllText($notes, $body.TrimEnd() + "`n")

Write-Host "5/6 Uploading ..."
gh release upload $tag -R adeeb111/AdeeboMeter (Join-Path $out "AdeeboMeter.exe") (Join-Path $out $win) (Join-Path $out "SHA256SUMS") --clobber
if ($LASTEXITCODE -ne 0) { throw "upload failed - the release still has GitHub's unsigned files; run this again" }
gh release edit $tag -R adeeb111/AdeeboMeter --notes-file $notes
if ($LASTEXITCODE -ne 0) { throw "the files are signed but the notes didn't update - paste $notes into the release by hand" }

Write-Host "6/6 Checking what's live ..."
$chk = Join-Path $work "check"; New-Item -ItemType Directory -Force $chk | Out-Null
gh release download $tag -R adeeb111/AdeeboMeter -D $chk -p "AdeeboMeter.exe" -p $win -p $src -p "SHA256SUMS"
$bad = 0
foreach ($l in Get-Content (Join-Path $chk "SHA256SUMS")) { $h, $n = $l -split "  ", 2; if ($n) { $ok = (Sha (Join-Path $chk $n)) -eq $h; Write-Host ("  " + $(if ($ok) { "OK  " } else { "BAD " }) + $n); if (-not $ok) { $bad++ } } }
$sig = Get-AuthenticodeSignature (Join-Path $chk "AdeeboMeter.exe")
$who = if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject.Split(',')[0] } else { "no signer" }   # bug-hunt ML-90: an unsigned or broken file has no certificate to name
Write-Host "  signature: $($sig.Status) - $who"
if ($bad -or $sig.Status -ne "Valid") { throw "something doesn't match - look before announcing the release" }
Write-Host "Done: $tag is signed. GitHub built $built, signed $signed."
