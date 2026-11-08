# Packages a release of AdeeboMeter into dist\ (run on Windows; GitHub Actions runs it for every published release).
#
#   dist\AdeeboMeter-<version>-win64.zip   AdeeboMeter\  AdeeboMeter.exe (installs itself), READ ME.txt,
#                                          AdeeboMeter - Theory of Everything.pdf, SHA256SUMS
#   dist\AdeeboMeter-<version>.zip         the source (the files git tracks)
#   dist\SHA256SUMS                        fingerprints of the exe and both zips
#   dist\RELEASE-NOTES-<version>.md        this version's CHANGELOG section
#
# The version comes from AdeeboMeter.cs (Program.Version) - change it there and add a CHANGELOG section, nothing else.
# The GitHub release is tagged v<version> (v1.0, v1.1 ...).
param([switch]$NoBuild)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$src = Get-Content -Raw "AdeeboMeter.cs"
$m = [regex]::Match($src, 'public const string Version = "([^"]+)"')
if (-not $m.Success) { throw "Version not found in AdeeboMeter.cs" }
$ver = $m.Groups[1].Value
Write-Host "AdeeboMeter $ver"

$log = Get-Content -Raw "CHANGELOG.md"
$i = $log.IndexOf("## $ver ")
if ($i -lt 0) { throw "CHANGELOG.md has no section '## $ver (date)' - add it first" }
$j = $log.IndexOf("`n## ", $i + 3)
$section = if ($j -gt 0) { $log.Substring($i, $j - $i) } else { $log.Substring($i) }

if (-not $NoBuild) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    & $csc /nologo /target:winexe /optimize+ /win32icon:assets\AdeeboMeter.ico /out:AdeeboMeter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll AdeeboMeter.cs
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
function Sha([string]$p) { (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant() }

$dist = Join-Path $here "dist"
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
$stage = Join-Path $dist "AdeeboMeter"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item AdeeboMeter.exe $stage
$t = (Get-Content -Raw "assets\READ ME.txt").Replace("__VERSION__", $ver) -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText((Join-Path $stage "READ ME.txt"), $t, (New-Object Text.UTF8Encoding $false))
Copy-Item "docs\AdeeboMeter-Theory-of-Everything.pdf" (Join-Path $stage "AdeeboMeter - Theory of Everything.pdf")
$exeSum = Sha (Join-Path $stage "AdeeboMeter.exe")
[IO.File]::WriteAllText((Join-Path $stage "SHA256SUMS"), "$exeSum  AdeeboMeter.exe`r`n")

$win = Join-Path $dist "AdeeboMeter-$ver-win64.zip"
Compress-Archive -Path $stage -DestinationPath $win -CompressionLevel Optimal
$srcZip = Join-Path $dist "AdeeboMeter-$ver.zip"
git archive --format=zip --prefix="AdeeboMeter-$ver/" -o $srcZip HEAD
if ($LASTEXITCODE -ne 0) { throw "git archive failed" }

$sums = "$exeSum  AdeeboMeter.exe`n$(Sha $win)  $(Split-Path -Leaf $win)`n$(Sha $srcZip)  $(Split-Path -Leaf $srcZip)`n"
[IO.File]::WriteAllText((Join-Path $dist "SHA256SUMS"), $sums)
Copy-Item (Join-Path $stage "AdeeboMeter.exe") $dist

$body = ($section -split "`n", 2)[1].Trim()
$notes = @"
# AdeeboMeter $ver

$body

**Download** ``AdeeboMeter-$ver-win64.zip``, open it, double-click **AdeeboMeter.exe** and choose *Install* - no administrator rights. Website: https://adeeb111.github.io/AdeeboMeter/
Assets: AdeeboMeter-$ver-win64.zip (the app and the Theory of Everything), AdeeboMeter.exe (the app alone), AdeeboMeter-$ver.zip (source), SHA256SUMS.
Built on GitHub's Windows runner with Microsoft's .NET Framework compiler from this exact source. Not code-signed: SmartScreen may say *More info > Run anyway*.

No cloud. No telemetry. Never listens. Created by Adeeb Khan.
"@
[IO.File]::WriteAllText((Join-Path $dist "RELEASE-NOTES-$ver.md"), $notes)
Remove-Item -Recurse -Force $stage
Get-ChildItem $dist | Format-Table Name, Length -AutoSize
Write-Host $sums
