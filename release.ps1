# Packages a release of AdeeboMeter into dist\ (run on Windows; GitHub Actions runs it for every published release).
#
#   dist\AdeeboMeter-<version>-win64.zip   AdeeboMeter\  AdeeboMeter.exe (installs itself), READ ME.txt,
#                                          AdeeboMeter - Theory of Everything.pdf, SHA256SUMS
#   dist\AdeeboMeter-<version>.zip         the source (the files git tracks)
#   dist\SHA256SUMS                        fingerprints of the exe and both zips
#   dist\RELEASE-NOTES-<version>.md        this version's CHANGELOG section
#
# The version comes from src\Program.cs (Program.Version) - change it there and add a CHANGELOG section, nothing else.
# The GitHub release is tagged v<version> (v1.0, v1.1 ...).
param([switch]$NoBuild)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$src = Get-Content -Raw "src\Program.cs"
$m = [regex]::Match($src, 'public const string Version = "([^"]+)"')
if (-not $m.Success) { throw "Version not found in src\Program.cs" }
$ver = $m.Groups[1].Value
Write-Host "AdeeboMeter $ver"

$log = Get-Content -Raw "CHANGELOG.md"
$i = $log.IndexOf("## $ver ")
if ($i -lt 0) { throw "CHANGELOG.md has no section '## $ver (date)' - add it first" }
$j = $log.IndexOf("`n## ", $i + 3)
$section = if ($j -gt 0) { $log.Substring($i, $j - $i) } else { $log.Substring($i) }

if (-not $NoBuild) {
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    # the knowledge (knowledge\*.txt) rides compressed inside the exe and is unpacked only when needed
    New-Item -ItemType Directory -Force obj | Out-Null
    $res = @()
    foreach ($k in Get-ChildItem knowledge\*.txt) {
        $b = [IO.File]::ReadAllBytes($k.FullName); $gz = "obj\$($k.BaseName).gz"
        $o = [IO.File]::Create($gz); $z = New-Object IO.Compression.GZipStream($o, [IO.Compression.CompressionLevel]::Optimal)
        $z.Write($b, 0, $b.Length); $z.Close(); $o.Close()
        $res += "/resource:$gz,AdeeboMeter.knowledge.$($k.BaseName).gz"
    }
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:assets\AdeeboMeter.ico /out:AdeeboMeter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Management.dll /r:System.Security.dll /recurse:src\*.cs @res
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
Copy-Item "LICENSE" (Join-Path $stage "LICENSE.txt")
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
$body = $body -replace "\r?\n  +(?=\S)", " "      # CHANGELOG wraps bullets at 120 columns; GitHub would show each wrap as a line break
$winName = Split-Path -Leaf $win; $srcName = Split-Path -Leaf $srcZip
$winMB = "{0:N1} MB" -f ((Get-Item $win).Length / 1MB)
$notes = @"
<p align="center"><img src="https://raw.githubusercontent.com/adeeb111/AdeeboMeter/main/docs/favicon.svg" width="72" alt=""></p>
<h1 align="center">AdeeboMeter $ver</h1>
<p align="center"><b>Is it me or the network?</b> One glance tells you.<br>No cloud. No telemetry. Never listens.</p>

## Download

| | File | For |
|---|---|---|
| ⭐ | **[$winName](https://github.com/adeeb111/AdeeboMeter/releases/download/v$ver/$winName)** ($winMB) | Everyone — the app, a READ ME and the Theory of Everything PDF |
| | [AdeeboMeter.exe](https://github.com/adeeb111/AdeeboMeter/releases/download/v$ver/AdeeboMeter.exe) | The app alone |
| | [$srcName](https://github.com/adeeb111/AdeeboMeter/releases/download/v$ver/$srcName) | Source code |

**Install in ten seconds:** open the zip → double-click **AdeeboMeter.exe** → **Install**. It installs just for you — no administrator rights — and starts in the bottom-right corner of your screen.
**Update:** run the new exe and choose **Update**; your settings come along. **Remove:** Settings › Apps › AdeeboMeter.

> Windows may show *"Windows protected your PC"* because AdeeboMeter isn't code-signed yet. Choose **More info → Run anyway**.

**Requirements:** Windows 10 or 11 (64-bit), .NET Framework 4.8 (built into Windows). Optional: Wireshark for live capture and *Find my switch port*.

$body

## Verify your download

Built by GitHub Actions on a clean Windows machine with Microsoft's own .NET Framework compiler, from this exact source.

| File | SHA256 |
|---|---|
| AdeeboMeter.exe | ``$exeSum`` |
| $winName | ``$(Sha $win)`` |
| $srcName | ``$(Sha $srcZip)`` |

In PowerShell: ``Get-FileHash .\AdeeboMeter.exe`` — the hash must match.

## Feedback

[Report a problem](https://github.com/adeeb111/AdeeboMeter/issues/new/choose) · [Ideas & questions](https://github.com/adeeb111/AdeeboMeter/discussions) · [Website & suggestion box](https://adeeb111.github.io/AdeeboMeter/#feedback) · [Full changelog](https://github.com/adeeb111/AdeeboMeter/blob/main/CHANGELOG.md)

<sub>Created by Adeeb Khan · MIT License.</sub>
"@
[IO.File]::WriteAllText((Join-Path $dist "RELEASE-NOTES-$ver.md"), $notes)
Remove-Item -Recurse -Force $stage
Get-ChildItem $dist | Format-Table Name, Length -AutoSize
Write-Host $sums
