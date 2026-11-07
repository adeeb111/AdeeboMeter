# Builds AdeeboMeter.exe with the C# compiler that ships inside Windows (.NET Framework 4.x) - no SDK, no admin.
# Then runs the self-test and starts the app.
param([switch]$NoTest, [switch]$NoStart)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
Get-Process AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force
& $csc /nologo /target:winexe /optimize+ "/out:$here\AdeeboMeter.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "$here\AdeeboMeter.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $here\AdeeboMeter.exe"
(Get-FileHash "$here\AdeeboMeter.exe" -Algorithm SHA256).Hash | ForEach-Object { Write-Host "SHA256 $_" }
if (-not $NoTest) {
  Start-Process "$here\AdeeboMeter.exe" -ArgumentList "--selftest" -Wait
  Get-Content "$here\AdeeboMeter-selftest.txt" | Select-String "^(PASS|FAIL)|ALL PASSED|FAILED"
}
if (-not $NoStart) { Start-Process "$here\AdeeboMeter.exe" }
