# rc-walk.ps1 - open every page with --view in each language, wait, screenshot the window (PrintWindow), and record the
# process's memory, handles, GDI and USER objects, and CPU used while the page sits idle for 5 s (redraw that never stops).
param([string]$Exe, [string]$Out, [int]$Wait = 7)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W { [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr p, int k); }
"@
[W]::SetProcessDPIAware() | Out-Null
$dir = Split-Path $Exe
$pages = "overview","guide","dest","toolbox","gamer","stream","sports","trade","outlook","security","travel","replay","changes","conn","link","procs","health","live","learn","windows","phone","points","lab","shell","settings","about","lang"
$rows = @("lang,page,ok,private_mb,handles,gdi,user,idle_cpu_ms_5s")
foreach ($lang in "en","fr","es","ar","bn") {
  Set-Content (Join-Path $dir "AdeeboMeter.settings") "setupdone=1`r`nlang=$lang`r`ntbview2=list"
  $shots = Join-Path $Out "walk-$lang"; New-Item -ItemType Directory $shots -Force | Out-Null
  foreach ($pg in $pages) {
    Get-Process AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800
    $p = Start-Process -FilePath $Exe -ArgumentList @("--view", $pg) -PassThru -WorkingDirectory $dir
    Start-Sleep -Seconds $Wait
    $w = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    if (-not $w -or $w.MainWindowHandle -eq 0) { $rows += "$lang,$pg,NO-WINDOW,,,,,"; continue }
    $c0 = $w.TotalProcessorTime.TotalMilliseconds; Start-Sleep -Seconds 5; $w.Refresh(); $c1 = $w.TotalProcessorTime.TotalMilliseconds
    $gdi = [W]::GetGuiResources($w.Handle, 0); $usr = [W]::GetGuiResources($w.Handle, 1)
    $r = New-Object W+R; [W]::GetWindowRect($w.MainWindowHandle, [ref]$r) | Out-Null
    $bw = $r.Rt - $r.L; $bh = $r.B - $r.T
    if ($bw -gt 50 -and $bh -gt 50) { $bmp = New-Object System.Drawing.Bitmap $bw, $bh; $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc(); [W]::PrintWindow($w.MainWindowHandle, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $bmp.Save((Join-Path $shots "$pg.png")); $g.Dispose(); $bmp.Dispose() }
    $rows += "$lang,$pg,ok,$([math]::Round($w.PrivateMemorySize64/1MB,1)),$($w.HandleCount),$gdi,$usr,$([math]::Round($c1-$c0))"
  }
}
Get-Process AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force
Set-Content (Join-Path $dir "AdeeboMeter.settings") "setupdone=1`r`nlang=en"
$rows | Set-Content (Join-Path $Out "walk.csv")
"walk: $(($rows.Count) - 1) pages measured" | Add-Content (Join-Path $Out "summary.txt")
