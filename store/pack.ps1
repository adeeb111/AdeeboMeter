# Builds the Microsoft Store packages from an AdeeboMeter.exe that release.ps1 already built and the self-test passed:
#   store\out\AdeeboMeter-<version>.msixbundle   upload this in Partner Center (Microsoft signs it after review)
# One AnyCPU exe, packaged twice - x64 and arm64 - and bundled, so Windows on ARM PCs get a native (not emulated) install.
# Needs the Windows SDK's makeappx / makepri (GitHub's windows-latest machines have them). Usage: store\pack.ps1 [path\to\AdeeboMeter.exe]
param([string]$Exe = "AdeeboMeter.exe")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root
if (-not (Test-Path $Exe)) { throw "$Exe not found - run release.ps1 first" }

$m = [regex]::Match((Get-Content -Raw "src\Program.cs"), 'public const string Version = "([^"]+)"')
if (-not $m.Success) { throw "Version not found in src\Program.cs" }
$parts = $m.Groups[1].Value.Split('.'); while ($parts.Count -lt 3) { $parts += "0" }
$ver = ($parts[0..2] -join ".") + ".0"          # the Store wants four numbers and the last one 0
Write-Host "Store package $ver"

$kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$tool = Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | Where-Object FullName -like "*\x64\*" | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $tool) { throw "makeappx.exe not found - install the Windows SDK" }
$bin = Split-Path $tool.FullName

$out = Join-Path $root "store\out"; $parts = Join-Path $root "store\obj\parts"
foreach ($d in $out, $parts) { if (Test-Path $d) { Remove-Item -Recurse -Force $d }; New-Item -ItemType Directory -Force $d | Out-Null }
foreach ($arch in "x64", "arm64") {
  $stage = Join-Path $root "store\obj\pkg-$arch"
  if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }; New-Item -ItemType Directory -Force $stage | Out-Null
  Copy-Item $Exe (Join-Path $stage "AdeeboMeter.exe")
  Copy-Item "store\images" (Join-Path $stage "images") -Recurse
  Copy-Item "LICENSE" (Join-Path $stage "LICENSE.txt")
  $man = (Get-Content -Raw "store\Package.appxmanifest").Replace("__VERSION__", $ver).Replace("__ARCH__", $arch)
  [IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $man, (New-Object Text.UTF8Encoding $false))
  # resources.pri lets Windows pick the right icon size (taskbar, Start, Settings)
  $cfg = Join-Path $root "store\obj\priconfig-$arch.xml"
  & "$bin\makepri.exe" createconfig /cf $cfg /dq en-US /pv 10.0.0 /o | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed" }
  & "$bin\makepri.exe" new /pr $stage /cf $cfg /mn (Join-Path $stage "AppxManifest.xml") /of (Join-Path $stage "resources.pri") /o
  if ($LASTEXITCODE -ne 0) { throw "makepri new failed" }
  & "$bin\makeappx.exe" pack /d $stage /p (Join-Path $parts "AdeeboMeter-$ver-$arch.msix") /o
  if ($LASTEXITCODE -ne 0) { throw "makeappx pack ($arch) failed" }
}
$bundle = Join-Path $out "AdeeboMeter-$ver.msixbundle"
& "$bin\makeappx.exe" bundle /d $parts /p $bundle /bv $ver /o
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed" }
Remove-Item -Recurse -Force (Join-Path $root "store\obj")
Get-Item $bundle | Format-Table Name, Length -AutoSize
