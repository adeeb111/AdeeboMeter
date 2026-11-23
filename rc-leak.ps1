# rc-leak.ps1 - one full view, Ctrl+1..7 and Ctrl+K-free page hopping for N minutes; memory, handles and GDI every 20 s.
# A steady climb is a leak; a plateau is fine.
param([string]$Exe, [string]$Out, [int]$Minutes = 10)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class L { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr p, int k); }
"@
$dir = Split-Path $Exe
Get-Process AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 1
$p = Start-Process -FilePath $Exe -ArgumentList @("--view", "overview") -PassThru -WorkingDirectory $dir; Start-Sleep 10
$rows = @("seconds,private_mb,handles,gdi,user")
$keys = "^1","^2","^3","^4","^5","^6","^7"; $t0 = Get-Date; $i = 0; $next = 0
while (((Get-Date) - $t0).TotalMinutes -lt $Minutes) {
  $w = Get-Process -Id $p.Id -ErrorAction SilentlyContinue; if (-not $w) { $rows += "CRASHED"; break }
  [L]::SetForegroundWindow($w.MainWindowHandle) | Out-Null
  [System.Windows.Forms.SendKeys]::SendWait($keys[$i % $keys.Count]); $i++; Start-Sleep -Milliseconds 1500
  $s = ((Get-Date) - $t0).TotalSeconds
  if ($s -ge $next) { $w.Refresh(); $rows += "$([math]::Round($s)),$([math]::Round($w.PrivateMemorySize64/1MB,1)),$($w.HandleCount),$([L]::GetGuiResources($w.Handle,0)),$([L]::GetGuiResources($w.Handle,1))"; $next += 20 }
}
Get-Process AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force
$rows | Set-Content (Join-Path $Out "leak.csv")
"leak run: $i page switches, $($rows.Count - 1) samples" | Add-Content (Join-Path $Out "summary.txt")
