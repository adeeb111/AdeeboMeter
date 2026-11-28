<#
  AdeeboMeter tester run (1.1.6 test3 and later)
  ------------------------------------------------
  Put this file next to AdeeboMeter.exe and run it:
      Right-click > Run with PowerShell
  or, in a PowerShell window in that folder:
      powershell -ExecutionPolicy Bypass -File .\Run-AdeeboMeterTests.ps1

  What it does (10 to 30 minutes - the self-test alone can take up to 25; no admin needed, nothing installed):
    0. Asks Windows Defender to scan AdeeboMeter.exe first (scan only - nothing is moved); a detection stops the run.
    1. The app's own self-test (every check it has, the shell's commands included).
    2. Pictures of every corner-widget style, folded and open, in four network states, light and dark, 100% and 150%.
    3. A live widget run: it starts a separate test copy of AdeeboMeter and moves your mouse and presses keys to check
       resting, Esc, dragging, clicks going through, a right-click / wheel passed on, and (on a touchscreen PC) taps.
       Hands off the mouse and keyboard during step 3 - about 3 minutes. It minimises your windows and puts them back.
  Your own AdeeboMeter, settings and history are never touched: the test copies keep everything in a _test folder here.
  If your AdeeboMeter is running, the script asks before closing it and starts it again at the end.

  At the end you get one zip on your Desktop: AdeeboMeter-test-results-<date>.zip. Send that zip back.

  Options:  -SkipLive  (no live widget run)    -SkipSelfTest    -SkipGallery    -SkipShots (no website pictures)
#>
param([switch]$SkipLive, [switch]$SkipSelfTest, [switch]$SkipGallery, [switch]$SkipShots)
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exeSrc = Join-Path $here 'AdeeboMeter.exe'
if (-not (Test-Path $exeSrc)) { Write-Host "AdeeboMeter.exe isn't next to this script ($here)." -ForegroundColor Red; Read-Host 'Press Enter to close'; exit 1 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$work = Join-Path $here "_test-$stamp"   # next to the script, not in %TEMP% - new programs started from %TEMP% look suspicious to antivirus
$out = Join-Path $work 'results'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$logFile = Join-Path $out 'results.txt'
$results = New-Object System.Collections.ArrayList

function Say([string]$m, [string]$color = 'Gray') { $l = (Get-Date -Format 'HH:mm:ss') + '  ' + $m; Write-Host $l -ForegroundColor $color; Add-Content -Path $logFile -Value $l -Encoding UTF8 }
function Result([string]$name, [bool]$pass, [string]$detail) {
    [void]$results.Add([pscustomobject]@{ Test = $name; Result = $(if ($pass) { 'PASS' } else { 'FAIL' }); Detail = $detail })
    Say ($(if ($pass) { 'PASS  ' } else { 'FAIL  ' }) + $name + $(if ($detail) { '  -  ' + $detail } else { '' })) $(if ($pass) { 'Green' } else { 'Red' })
}
function Note([string]$name, [string]$detail) { [void]$results.Add([pscustomobject]@{ Test = $name; Result = 'INFO'; Detail = $detail }); Say ('INFO  ' + $name + '  -  ' + $detail) 'Cyan' }
# hunters15 T-ML5: one guard for the whole script - a step that throws is recorded as a FAIL and the script carries on, so your
# own AdeeboMeter is always started again and the zip is always made
trap { if ($_.Exception -is [System.Management.Automation.PipelineStoppedException]) { break }; Result 'Script step' $false ($_.InvocationInfo.ScriptLineNumber.ToString() + ': ' + $_.Exception.Message); continue }

# a fresh test copy: the exe, the portable marker (all data stays in that folder) and a settings file so no first-run window shows
function New-TestCopy([string]$dir, [string[]]$settings) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item $exeSrc (Join-Path $dir 'AdeeboMeter.exe') -Force
    Set-Content -Path (Join-Path $dir 'AdeeboMeter.portable') -Value '' -Encoding ASCII
    if ($settings) { Set-Content -Path (Join-Path $dir 'AdeeboMeter.settings') -Value $settings -Encoding UTF8 }
    return (Join-Path $dir 'AdeeboMeter.exe')
}
function Run-Timed([string]$exe, [string]$argLine, [int]$seconds) {
    $p = Start-Process -FilePath $exe -ArgumentList $argLine -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit($seconds * 1000)) { try { $p.Kill() } catch { }; return -999 }
    return $p.ExitCode
}

# ---------------------------------------------------------------- the machine
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class W {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(int flags, int dx, int dy, int data, IntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, int flags, IntPtr extra);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int i);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct PT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RC { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINTER_INFO { public int pointerType; public uint pointerId, frameId; public int pointerFlags; public IntPtr sourceDevice, hwndTarget; public PT ptPixelLocation, ptHimetricLocation, ptPixelLocationRaw, ptHimetricLocationRaw; public uint dwTime, historyCount; public int inputData; public uint dwKeyStates; public ulong PerformanceCount; public int ButtonChangeType; }
    [StructLayout(LayoutKind.Sequential)] public struct POINTER_TOUCH_INFO { public POINTER_INFO pointerInfo; public int touchFlags, touchMask; public RC rcContact, rcContactRaw; public uint orientation, pressure; }
    [DllImport("user32.dll", SetLastError = true)] public static extern bool InitializeTouchInjection(uint max, int mode);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool InjectTouchInput(uint n, [In] POINTER_TOUCH_INFO[] c);
    public static bool TouchReady;
    public static bool Touch(int x, int y, bool down) {
        var t = new POINTER_TOUCH_INFO(); t.pointerInfo.pointerType = 2; t.pointerInfo.pointerId = 0;
        t.pointerInfo.ptPixelLocation.X = x; t.pointerInfo.ptPixelLocation.Y = y;
        t.pointerInfo.pointerFlags = down ? (0x10000 | 0x2 | 0x4) : 0x40000;
        return Send(t, x, y);
    }
    public static bool TouchMove(int x, int y) {   // the finger moving while down (hunters14 LAB-ML3: the touch drag)
        var t = new POINTER_TOUCH_INFO(); t.pointerInfo.pointerType = 2; t.pointerInfo.pointerId = 0;
        t.pointerInfo.ptPixelLocation.X = x; t.pointerInfo.ptPixelLocation.Y = y;
        t.pointerInfo.pointerFlags = 0x20000 | 0x2 | 0x4;
        return Send(t, x, y);
    }
    static bool Send(POINTER_TOUCH_INFO t, int x, int y) {
        t.touchMask = 1 | 2 | 4; t.orientation = 90; t.pressure = 32000;
        t.rcContact.L = x - 2; t.rcContact.R = x + 2; t.rcContact.T = y - 2; t.rcContact.B = y + 2;
        return InjectTouchInput(1, new[] { t });
    }
}
'@
[void][W]::SetProcessDPIAware()   # the same pixels the app uses
$dc = [W]::GetDC([IntPtr]::Zero); $dpi = [W]::GetDeviceCaps($dc, 88); [void][W]::ReleaseDC([IntPtr]::Zero, $dc)
Say "AdeeboMeter test run $stamp"
Say ("Windows " + [Environment]::OSVersion.Version + ", PowerShell " + $PSVersionTable.PSVersion + ", scaling " + [math]::Round($dpi / 96 * 100) + "%, screens: " + (([System.Windows.Forms.Screen]::AllScreens | ForEach-Object { $_.Bounds.Width.ToString() + 'x' + $_.Bounds.Height + $(if ($_.Primary) { ' (main)' } else { '' }) }) -join ', ') + ", touch: " + $(if (([W]::GetSystemMetrics(94) -band 0x40) -ne 0) { 'yes' } else { 'no' }))
Say ("AdeeboMeter.exe " + (Get-Item $exeSrc).VersionInfo.FileVersion + ", " + [math]::Round((Get-Item $exeSrc).Length / 1MB, 1) + " MB, SHA-256 " + (Get-FileHash $exeSrc -Algorithm SHA256).Hash)

# ---------------------------------------------------------------- 0. Defender's opinion of the exe, before anything runs
$mp = @((Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'), $(if ($env:ProgramW6432) { Join-Path $env:ProgramW6432 'Windows Defender\MpCmdRun.exe' })) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1   # hunters15 T-L3: 32-bit PowerShell sees the x86 folder
if ($mp) {
    Say 'Step 0: Windows Defender scans AdeeboMeter.exe (scan only)...' 'Yellow'
    $scan = & $mp -Scan -ScanType 3 -File $exeSrc -DisableRemediation 2>&1 | Out-String
    Set-Content -Path (Join-Path $out 'defender-scan.txt') -Value $scan -Encoding UTF8
    $threat = $scan -match 'found \d+ threats?' -and $scan -notmatch 'found no threats'
    if ($LASTEXITCODE -eq 2 -and -not $threat) { Result 'Windows Defender scan' $false ('the scan itself failed (exit 2, no threat named) - see defender-scan.txt') }
    elseif ($LASTEXITCODE -eq 2) {
        Result 'Windows Defender scan' $false (($scan -split "`n" | Where-Object { $_ -match 'Threat|found' } | Select-Object -First 3) -join ' ')
        Say 'Defender flags this exe - stopping here (nothing was run). Send the zip from your Desktop.' 'Red'
        $results | Export-Csv -Path (Join-Path $out 'results.csv') -NoTypeInformation -Encoding UTF8
        $zip0 = Join-Path ([Environment]::GetFolderPath('Desktop')) "AdeeboMeter-test-results-$stamp.zip"; Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip0 -Force
        Write-Host "Results: $zip0" -ForegroundColor Green; Read-Host 'Press Enter to close'; exit 2
    }
    else { Result 'Windows Defender scan' ($LASTEXITCODE -eq 0) $(if ($LASTEXITCODE -ne 0) { "MpCmdRun exit $LASTEXITCODE" } else { 'no threats' }) }
} else { Note 'Windows Defender scan' 'NOT RUN - MpCmdRun.exe not found (another antivirus may be in charge). Scan AdeeboMeter.exe by hand before trusting this run.' }

# ---------------------------------------------------------------- 1. the self-test
if (-not $SkipSelfTest) {
    Say 'Step 1 of 4: the self-test (up to 25 minutes)...' 'Yellow'
    $st = New-TestCopy (Join-Path $work 'selftest') $null
    $rc = Run-Timed $st '--selftest' 1500
    $rep = Get-ChildItem (Split-Path $st) -Recurse -Filter 'AdeeboMeter-selftest.txt' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-30) } | Sort-Object LastWriteTime | Select-Object -Last 1   # hunters14 LAB-L2: the test copy's own report only - never one from your installed AdeeboMeter
    if (-not (Test-Path $st)) { Result 'Self-test copy' $false 'the test copy of AdeeboMeter.exe is gone - antivirus removed it?' }
    if ($rep) { Copy-Item $rep.FullName (Join-Path $out 'selftest.txt'); $fails = @(Select-String -Path $rep.FullName -Pattern '^FAIL' ).Count; Result 'Self-test' ($fails -eq 0) ("$fails failed" + $(if ($rc -eq -999) { ', stopped after 25 minutes' } else { '' })) }
    else { Result 'Self-test' $false "no report (exit code $rc)" }
    Get-ChildItem (Split-Path $st) -Filter 'AdeeboMeter.log*' -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName (Join-Path $out ('selftest-' + $_.Name)) }
    # the sample ISP report the self-test draws (made-up numbers) - so the new report can be checked as Windows renders it
    $isp = Get-ChildItem (Split-Path $st) -Recurse -Filter 'ISP-report-*.html' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($isp) { Copy-Item $isp.FullName (Join-Path $out 'sample-isp-report.html'); Result 'Sample ISP report' ((Get-Content $isp.FullName -Raw) -match '<svg') 'drawn by the self-test' } else { Note 'Sample ISP report' 'not found next to the self-test copy' }
}

# ---------------------------------------------------------------- 2. the widget pictures
if (-not $SkipGallery) {
    Say 'Step 2 of 4: drawing every widget style (about a minute)...' 'Yellow'
    $gx = New-TestCopy (Join-Path $work 'gallery') @('setupdone=1', 'lang=en', 'buddy=0')
    $gout = Join-Path $out 'widgets'
    $rc = Run-Timed $gx ('--widget-gallery "' + $gout + '"') 300
    $sheets = @(Get-ChildItem $gout -Filter '*.png' -ErrorAction SilentlyContinue).Count
    $cards = @(Get-ChildItem (Join-Path $gout 'cards') -Filter '*.png' -ErrorAction SilentlyContinue).Count
    Result 'Widget pictures' ($rc -eq 0 -and $sheets -eq 6 -and $cards -gt 0) "exit $rc, $sheets of 6 sheets, $cards single cards at 200%"
    if (Test-Path (Join-Path $gout 'widget-sizes.csv')) {
        $tall = @(Import-Csv (Join-Path $gout 'widget-sizes.csv') | Where-Object { $_.card_h -and [int]$_.card_h -ge 900 })
        Result 'No card hits the 900 px cap' ($tall.Count -eq 0) $(if ($tall.Count) { ($tall | Select-Object -First 6 | ForEach-Object { $_.theme + ' ' + $_.scale + ' ' + $_.style + ' ' + $_.state }) -join '; ' } else { '' })
    } else { Result 'No card hits the 900 px cap' $false 'widget-sizes.csv was not written - the gallery stopped early' }   # hunters15 T-L7
}

# ---------------------------------------------------------------- 3. the live widget
if ($SkipLive) { Say 'Live widget run skipped (-SkipLive).' }
else {
    Say 'Step 3 of 4: the live widget run.' 'Yellow'
    $mine = @(Get-Process -Name 'AdeeboMeter' -ErrorAction SilentlyContinue)
    $restart = @()
    if ($mine.Count -gt 0) {
        $a = Read-Host 'AdeeboMeter is running. Close it for the test? It starts again at the end. [Y/n]'
        if ($a -match '^[Nn]') { Say 'Kept running - the live run needs it closed, so it was skipped.' 'Yellow'; $SkipLive = $true }
        else { foreach ($p in $mine) { try { $restart += $p.Path } catch { } ; Stop-Process -Id $p.Id -Force }; Start-Sleep -Seconds 2 }
    }
}
if (-not $SkipLive) {
    Write-Host ''
    Write-Host 'Hands off the mouse and keyboard for about 3 minutes, starting in 5 seconds.' -ForegroundColor Yellow
    Write-Host '(Your windows are minimised for the test and come back at the end.)' -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    $live = Join-Path $work 'live'
    $lx = New-TestCopy $live @('autostart=0', 'widget=1', 'serious=0', 'theme=light', 'setupdone=1', 'setupstep=3', 'wstyle=everyday', 'whint=1', 'whidenear=0', 'gwidget=0', 'lang=en', 'dwell2=450', 'buddy=0', 'wsince=1', 'fade=77')
    $trace = Join-Path $live 'widget-trace.txt'
    $shell = New-Object -ComObject Shell.Application
    $app = $null; $sink = $null; $target = $null
    $script:got = New-Object System.Collections.ArrayList
    try { [Console]::TreatControlCAsInput = $true } catch { }   # an injected Ctrl+C can't stop the script
    function Pump([int]$ms) { $end = (Get-Date).AddMilliseconds($ms); while ((Get-Date) -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 15 } }
    function Lines() { if (Test-Path $trace) { @(Get-Content $trace -Encoding UTF8) } else { @() } }
    function Mark() { @(Lines).Count }
    function Since([int]$m) { $l = @(Lines); if ($l.Count -gt $m) { $l[$m..($l.Count - 1)] } else { @() } }
    function Seen([int]$m, [string]$rx) { @(Since $m | Where-Object { $_ -match $rx }).Count -gt 0 }
    function WaitFor([int]$m, [string]$rx, [int]$ms) { $end = (Get-Date).AddMilliseconds($ms); while ((Get-Date) -lt $end) { if (Seen $m $rx) { return $true }; Pump 50 }; return $false }
    function Rect() {
        $r = Lines | Where-Object { $_ -match ' rect (-?\d+) (-?\d+) (\d+) (\d+)' } | Select-Object -Last 1
        if (-not $r -or $r -notmatch ' rect (-?\d+) (-?\d+) (\d+) (\d+)') { return $null }
        [pscustomobject]@{ X = [int]$Matches[1]; Y = [int]$Matches[2]; W = [int]$Matches[3]; H = [int]$Matches[4]; CX = [int]$Matches[1] + [int]([int]$Matches[3] / 2); CY = [int]$Matches[2] + [int]([int]$Matches[4] / 2); Line = $r }
    }
    function Pos() { [System.Windows.Forms.Cursor]::Position }
    function Glide([int]$x, [int]$y, [int]$steps, [int]$stepMs) { $p = Pos; for ($i = 1; $i -le $steps; $i++) { [void][W]::SetCursorPos([int]($p.X + ($x - $p.X) * $i / $steps), [int]($p.Y + ($y - $p.Y) * $i / $steps)); Pump $stepMs } }
    # onto the widget the way a hand does: quickly to near it, then slowly through the last 200 px
    function Arrive($r) { $dx = $(if ($r.CX -gt $hm.X) { -1 } else { 1 }); $dy = $(if ($r.CY -gt $hm.Y) { -1 } else { 1 }); Glide ($r.CX + $dx * 260) ($r.CY + $dy * 200) 10 20; Glide $r.CX $r.CY 40 45 }
    function GoHome() { Glide $hm.X $hm.Y 12 20; Pump 1500 }
    function Key([byte]$vk, [bool]$down) { [W]::keybd_event($vk, 0, $(if ($down) { 0 } else { 2 }), [IntPtr]::Zero) }
    function Tap([byte]$vk) { Key $vk $true; Pump 150; Key $vk $false }
    function Mouse([int]$flags, [int]$data) { [W]::mouse_event($flags, 0, 0, $data, [IntPtr]::Zero) }
    function Shot([string]$name) { try { ShotNow $name } catch { Note ('Picture ' + $name) ('not taken: ' + $_.Exception.Message) } }   # hunters15 T-ML6: a lost screen doesn't end the run
    function ShotNow([string]$name) {
        $r = Rect; if (-not $r) { return }
        $b = [System.Drawing.Rectangle]::FromLTRB($r.X - 30, $r.Y - 30, $r.X + $r.W + 30, $r.Y + $r.H + 30)
        $b = [System.Drawing.Rectangle]::Intersect($b, [System.Windows.Forms.SystemInformation]::VirtualScreen); if ($b.Width -lt 2 -or $b.Height -lt 2) { return }
        $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height; $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size); $g.Dispose(); $bmp.Save((Join-Path $out ("live-$name.png")), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    }
    function Unlocked() { $r = Rect; $r -and $r.Line -match 'unlocked=True' }
    function Alive() { $app -and -not $app.HasExited }
    # hunters15 T-ML1: a "nothing happened" check counts only when the widget was locked before it and the test copy is still alive
    function Negative([string]$name, [int]$m, [bool]$wasLocked) {
        if (-not (Alive)) { Result $name $false 'the test copy had stopped - absence proves nothing' }
        elseif (-not $wasLocked) { Result $name $false 'it was already unlocked before this step - an earlier step failed to lock it' }
        else { Result $name (-not (Seen $m ' unlock \w')) '' }
    }
    # hunters15 T-ML4: every injected touch is checked - a refused one fails the step instead of passing quietly
    function T([int]$x, [int]$y, [bool]$down) { if (-not [W]::Touch($x, $y, $down)) { throw "Windows refused a simulated touch at $x,$y" } }
    function TM([int]$x, [int]$y) { if (-not [W]::TouchMove($x, $y)) { throw "Windows refused a simulated touch move to $x,$y" } }
    function NewForm([System.Drawing.Rectangle]$b, [string]$text) {
        $f = New-Object System.Windows.Forms.Form; $f.FormBorderStyle = 'None'; $f.StartPosition = 'Manual'; $f.ShowInTaskbar = $false; $f.Bounds = $b; $f.BackColor = [System.Drawing.Color]::FromArgb(250, 240, 200); $f.Text = $text
        $f.Show(); Pump 300; return $f
    }
    try {
        $shell.MinimizeAll(); Pump 1500
        $app = Start-Process -FilePath $lx -ArgumentList '--widget-trace' -WorkingDirectory $live -PassThru
        if (-not (WaitFor 0 ' rect ' 60000)) { throw 'The test copy never showed its widget (no "rect" in widget-trace.txt after 60 s).' }
        Pump 4000
        $r0 = Rect; Say ('Widget at ' + $r0.X + ',' + $r0.Y + ' size ' + $r0.W + 'x' + $r0.H)
        $scr = [System.Windows.Forms.Screen]::FromPoint((New-Object System.Drawing.Point $r0.CX, $r0.CY)).WorkingArea
        $hm = New-Object System.Drawing.Point ([int]($scr.X + $scr.Width / 2)), ([int]($scr.Y + $scr.Height / 2))
        # a small window far from the widget takes the keyboard, so injected keys land somewhere harmless
        $sx = $(if ($r0.CX -gt $hm.X) { $scr.X + 40 } else { $scr.Right - 260 }); $sy = $(if ($r0.CY -gt $hm.Y) { $scr.Y + 40 } else { $scr.Bottom - 140 })
        $sink = NewForm (New-Object System.Drawing.Rectangle $sx, $sy, 220, 100) 'AdeeboMeter test - keys land here'
        GoHome; Shot 'folded'

        # --- resting unlocks; leaving locks
        $sink.Activate(); $m = Mark; Arrive (Rect); Shot 'open'
        $ok = WaitFor $m 'unlock rest' 5000; Result 'Resting still on it unlocks it (2.5 s)' $ok ''
        if ($ok) { Shot 'unlocked' }
        $m = Mark; GoHome; Result 'Leaving it locks it again' (Seen $m 'lock gone') ''

        # --- a jump onto it, then a small one (what two finger taps do to the pointer): no unlock
        $r = Rect; $lk = -not (Unlocked); $m = Mark; [void][W]::SetCursorPos($r.CX, $r.CY); Pump 600; [void][W]::SetCursorPos($r.CX + 6, $r.CY + 4); Pump 3600
        Negative 'Two jumps onto it (like two taps) do not unlock it' $m $lk
        GoHome

        # --- real finger taps, where Windows allows touch to be simulated
        try { [W]::TouchReady = [W]::InitializeTouchInjection(1, 3) } catch { [W]::TouchReady = $false }
        if ([W]::TouchReady) {
            $r = Rect; $lk = -not (Unlocked); $m = Mark
            try {
                T $r.CX $r.CY $true; Pump 80; T $r.CX $r.CY $false; Pump 500
                T ($r.CX + 6) ($r.CY + 4) $true; Pump 80; T ($r.CX + 6) ($r.CY + 4) $false; Pump 3600
                # T-ML4: without Windows' touch flag this was two mouse jumps again - said as not tested, never a PASS
                if (Seen $m 'touch cursor True') { Negative 'Two finger taps do not unlock it' $m $lk } else { Note 'Two finger taps do not unlock it' 'NOT TESTED - Windows never flagged the pointer as touch; test by hand on a touchscreen' }
            } catch { Result 'Two finger taps do not unlock it' $false $_.Exception.Message }
            Mouse 0x1 0; Mouse 0x0001 0; for ($i = 0; $i -lt 6; $i++) { [W]::mouse_event(0x0001, 3, 0, 0, [IntPtr]::Zero); Pump 30; [W]::mouse_event(0x0001, -3, 0, 0, [IntPtr]::Zero); Pump 30 }   # a mouse move: Windows shows the pointer again
            GoHome
        } else { Note 'Finger taps' 'Windows did not allow simulated touch here - test by hand on a touchscreen' }

        # --- Esc locks
        $sink.Activate(); $m = Mark; Arrive (Rect)
        if (WaitFor $m 'unlock rest' 5000) { $m2 = Mark; Tap 0x1B; Result 'Esc locks it' (WaitFor $m2 'lock esc' 1500) '' } else { Result 'Esc locks it' $false 'could not unlock it first' }
        GoHome

        # --- a drag moves it and the place is kept; Esc mid-drag puts it back
        $m = Mark; $r = Rect; Arrive $r
        if (WaitFor $m 'unlock rest' 5000) {
            $before = Rect; $p0 = Pos; $m2 = Mark; Mouse 0x2 0; Pump 120; Glide ($p0.X - 200) ($p0.Y - 120) 20 25;   # from where the pointer is (test5: it measured from the card's middle, but the pointer sat lower, where the chip was)
            Pump 150; Mouse 0x4 0; Pump 600
            $after = Rect; $moved = $after -and [math]::Abs(($before.X - $after.X) - 200) -le 30 -and [math]::Abs(($before.Y - $after.Y) - 120) -le 30
            Result 'Dragging moves it and keeps the place' ((Seen $m2 'drag saved') -and $moved) $(if ($after) { 'from ' + $before.X + ',' + $before.Y + ' to ' + $after.X + ',' + $after.Y } else { '' })
            Shot 'dragged'
        } else { Result 'Dragging moves it and keeps the place' $false 'could not unlock it first' }
        GoHome
        $m = Mark; Arrive (Rect)
        if (WaitFor $m 'unlock rest' 5000) {
            $before = Rect; $m2 = Mark; Mouse 0x2 0; Pump 120; Glide ($before.CX + 150) ($before.CY) 15 25; Pump 200; Tap 0x1B; Pump 300; Mouse 0x4 0; Pump 800
            $after = Rect; Result 'Esc mid-drag puts it back and locks it' ((Seen $m2 'drag cancelled by Esc') -and (Seen $m2 'lock esc') -and $after -and [math]::Abs($after.X - $before.X) -le 8) $(if ($after) { 'back at ' + $after.X + ',' + $after.Y + ' (was ' + $before.X + ',' + $before.Y + ')' } else { '' })
        } else { Result 'Esc mid-drag puts it back and locks it' $false 'could not unlock it first' }
        GoHome

        # --- a window under the widget: clicks go through when locked; right-click and wheel are passed on when unlocked;
        #     resting with a button held doesn't unlock
        $r = Rect; $tb = [System.Drawing.Rectangle]::FromLTRB($r.X - 80, $r.Y - 80, $r.X + $r.W + 80, $r.Y + $r.H + 80)
        $target = NewForm $tb 'AdeeboMeter test - the app under the widget'
        $target.Add_MouseDown({ param($s, $e) [void]$script:got.Add('down ' + $e.Button) }); $target.Add_MouseUp({ param($s, $e) [void]$script:got.Add('up ' + $e.Button) }); $target.Add_MouseWheel({ param($s, $e) [void]$script:got.Add('wheel ' + $e.Delta) })
        $target.Activate(); Pump 1500
        $r = Rect; $script:got.Clear(); $m = Mark; [void][W]::SetCursorPos($r.CX, $r.CY); Pump 150; Mouse 0x2 0; Pump 60; Mouse 0x4 0; Pump 500
        Result 'Locked: a click goes through to the app underneath' (($script:got -contains 'down Left') -and -not (Seen $m 'open full')) ('the app got: ' + ($script:got -join ', '))
        GoHome
        $script:got.Clear(); $m = Mark; Arrive (Rect)
        if (WaitFor $m 'unlock rest' 5000) {
            $m2 = Mark; Mouse 0x8 0; Pump 100; Mouse 0x10 0; Pump 600
            Result 'Unlocked: a right-click locks it and reaches the app underneath' ((Seen $m2 'replay Right') -and ($script:got -contains 'down Right') -and ($script:got -contains 'up Right')) ('the app got: ' + ($script:got -join ', '))
        } else { Result 'Unlocked: a right-click locks it and reaches the app underneath' $false 'could not unlock it over a window (covered corner)' }
        GoHome
        $script:got.Clear(); $m = Mark; Arrive (Rect)
        if (WaitFor $m 'unlock rest' 5000) {
            $m2 = Mark; Mouse 0x800 -120; Pump 600
            Result 'Unlocked: the wheel locks it and scrolls the app underneath' ((Seen $m2 'replay wheel') -and (@($script:got | Where-Object { $_ -like 'wheel*' }).Count -gt 0)) ('the app got: ' + ($script:got -join ', '))
        } else { Result 'Unlocked: the wheel locks it and scrolls the app underneath' $false 'could not unlock it first' }
        GoHome
        $r = Rect; $lk = -not (Unlocked); $m = Mark; [void][W]::SetCursorPos($tb.X + 20, $tb.Y + 20); Pump 300; Mouse 0x2 0; Pump 100; Glide $r.CX $r.CY 30 40; Pump 3600
        Negative 'Resting with a button held (a drag in the app underneath) does not unlock it' $m $lk
        Mouse 0x4 0; Pump 200
        $target.Close(); $target = $null; GoHome

        # --- an unlocked click opens AdeeboMeter (last: it opens the full window)
        $m = Mark; Arrive (Rect)
        if (WaitFor $m 'unlock rest' 5000) { $m2 = Mark; Mouse 0x2 0; Pump 60; Mouse 0x4 0; Result 'Unlocked: a click opens AdeeboMeter' (WaitFor $m2 'open full' 1500) '' }
        else { Result 'Unlocked: a click opens AdeeboMeter' $false 'could not unlock it first' }
        Pump 2500; $m3 = Mark; Tap 0x1B   # Esc: the full window goes back to the corner - checked (hunters15 T-ML3): the widget draws again
        if (-not (WaitFor $m3 ' rect ' 4000)) { Tap 0x1B; if (-not (WaitFor $m3 ' rect ' 4000)) { Result 'Esc closes the full window' $false 'the widget did not come back - the tray and finger checks below may fail for this reason' } else { Result 'Esc closes the full window' $true 'needed a second Esc (something was open on the page)' } } else { Result 'Esc closes the full window' $true '' }

        # --- tray > Move widget (the only unlock for touch) - asked for through the trace's file hook, the menu item's own code
        GoHome; $m = Mark; Set-Content -Path (Join-Path $live 'move-widget.now') -Value '' -Encoding ASCII
        Result 'Tray: Move widget unlocks it' (WaitFor $m 'unlock tray' 4000) ''
        # --- a finger drag on the unlocked widget moves it, and a finger tap on it opens AdeeboMeter
        if ([W]::TouchReady -and (Seen $m 'unlock tray')) {
            $r = Rect; $m = Mark
            try {
                T $r.CX $r.CY $true; Pump 80
                for ($i = 1; $i -le 15; $i++) { TM ($r.CX - 10 * $i) ($r.CY - 6 * $i); Pump 30 }
                T ($r.CX - 150) ($r.CY - 90) $false; Pump 800
                $a2 = Rect; $mv = $a2 -and [math]::Abs(($r.X - $a2.X) - 150) -le 40 -and [math]::Abs(($r.Y - $a2.Y) - 90) -le 40   # hunters15 T-L1: where it went, not only that it was traced
                Result 'A finger drag moves it' ((Seen $m 'touch grab') -and (Seen $m 'drag saved') -and $mv) $(if ($a2) { 'from ' + $r.X + ',' + $r.Y + ' to ' + $a2.X + ',' + $a2.Y } else { '' })
            } catch { Result 'A finger drag moves it' $false $_.Exception.Message }
            $m = Mark; Set-Content -Path (Join-Path $live 'move-widget.now') -Value '' -Encoding ASCII
            # hunters15 T-ML2: a still tap becomes a mouse click; a 2 px wobble (under the 4 px drag) makes it a real touch tap, and
            # only the touch path's own trace counts
            if (WaitFor $m 'unlock tray' 4000) { $r = Rect; $m2 = Mark; try { T $r.CX $r.CY $true; Pump 40; TM ($r.CX + 2) ($r.CY + 1); Pump 40; T ($r.CX + 2) ($r.CY + 1) $false; Result 'A finger tap on the unlocked widget opens AdeeboMeter' (WaitFor $m2 'open full \(tap\)' 2000) $(if (Seen $m2 'open full$') { 'opened as a mouse click, not the touch tap' } else { '' }) } catch { Result 'A finger tap on the unlocked widget opens AdeeboMeter' $false $_.Exception.Message }; Pump 2000; Tap 0x1B; Pump 1500 }
            else { Result 'A finger tap on the unlocked widget opens AdeeboMeter' $false 'could not unlock it first' }
            Mouse 0x1 0; for ($i = 0; $i -lt 6; $i++) { [W]::mouse_event(0x0001, 3, 0, 0, [IntPtr]::Zero); Pump 30; [W]::mouse_event(0x0001, -3, 0, 0, [IntPtr]::Zero); Pump 30 }
        } elseif (-not [W]::TouchReady) { Note 'Finger drag and tap' 'Windows did not allow simulated touch here - test by hand on a touchscreen' }
        Note 'First run' 'the first-run card and setup are not driven by this script - check them by hand on a fresh install'
    }
    catch { Result 'Live widget run' $false $_.Exception.Message }
    finally {
        foreach ($vk in 0x11, 0x10, 0x12, 0x43, 0x56, 0x1B) { Key $vk $false }; Mouse 0x4 0; Mouse 0x10 0
        if ($target) { try { $target.Close() } catch { } }; if ($sink) { try { $sink.Close() } catch { } }
        if ($app -and -not $app.HasExited) { try { Stop-Process -Id $app.Id -Force } catch { } }
        Pump 500; try { $shell.UndoMinimizeALL() } catch { }
        try { [Console]::TreatControlCAsInput = $false } catch { }
        foreach ($f in 'widget-trace.txt', 'AdeeboMeter.log', 'AdeeboMeter.settings') { $p = Join-Path $live $f; if (Test-Path $p) { Copy-Item $p (Join-Path $out ('live-' + $f)) } }
    }
}

# ---------------------------------------------------------------- 4. pictures for the website (your own data - they stay on your Desktop)
# The pages are drawn from this PC's real history (Gamer, Streaming, Markets), so they are NOT put in the results zip: look at them
# first and send only what you're happy to share. Needs AdeeboMeter closed (one copy at a time).
if (-not $SkipShots) {
    Write-Host ''
    $a = Read-Host 'Step 4: take 3 pictures of AdeeboMeter for the website, with your real data, in the dark theme? They go to a folder on your Desktop, not in the zip. [y/N]'
    if ($a -match '^[Yy]') {
        foreach ($p in @(Get-Process -Name 'AdeeboMeter' -ErrorAction SilentlyContinue)) { try { if (-not ($restart -contains $p.Path)) { $restart += $p.Path }; Stop-Process -Id $p.Id -Force } catch { } }
        Start-Sleep -Seconds 2
        $shotDir = Join-Path ([Environment]::GetFolderPath('Desktop')) "AdeeboMeter-website-pictures-$stamp"; New-Item -ItemType Directory -Force -Path $shotDir | Out-Null
        # no portable marker: it starts like your own AdeeboMeter, on your real data folder - it draws from your history, and like any
        # normal start it may also send reports waiting in the outbox and add to the log (hunters15 T-L4: not "only reads")
        $sx = Join-Path $work 'shots'; New-Item -ItemType Directory -Force -Path $sx | Out-Null; Copy-Item $exeSrc (Join-Path $sx 'AdeeboMeter.exe') -Force
        foreach ($pg in 'gamer', 'stream', 'trade', 'health') {
            $f = Join-Path $sx "shot-$pg.png"; if (Test-Path $f) { Remove-Item $f -Force }
            $sp = Start-Process -FilePath (Join-Path $sx 'AdeeboMeter.exe') -ArgumentList "--dark --view shot:$pg" -WorkingDirectory $sx -PassThru
            # hunters15 T-ML5: wait until the file has stopped growing - the app creates it before it has written it
            $t0 = Get-Date; $last = -1; while (((Get-Date) - $t0).TotalSeconds -lt 40) { if (Test-Path $f) { $len = (Get-Item $f).Length; if ($len -gt 0 -and $len -eq $last) { break }; $last = $len }; Start-Sleep -Milliseconds 500 }
            Start-Sleep -Milliseconds 800; try { Stop-Process -Id $sp.Id -Force } catch { }; Start-Sleep -Seconds 2
            if (Test-Path $f) {
                try {
                    Copy-Item $f (Join-Path $shotDir "shot-$pg.png") -Force; $img = New-Object System.Drawing.Bitmap $f
                    # hunters15 T-L2: wide and tall enough, and not one flat colour (a blank or still-loading page)
                    $cols = @{}; for ($yy = 0; $yy -lt $img.Height; $yy += [math]::Max(1, [int]($img.Height / 12))) { for ($xx = 0; $xx -lt $img.Width; $xx += [math]::Max(1, [int]($img.Width / 12))) { $cols[$img.GetPixel($xx, $yy).ToArgb()] = 1 } }
                    Result ("Website picture: " + $pg) ($img.Width -ge 1200 -and $img.Height -ge 700 -and $cols.Count -ge 6) ("" + $img.Width + 'x' + $img.Height + ' px, ' + $cols.Count + ' colours sampled'); $img.Dispose()
                } catch { Result ("Website picture: " + $pg) $false ('could not read the picture: ' + $_.Exception.Message) }
            }
            else { Result ("Website picture: " + $pg) $false 'no picture after 40 s' }
        }
        Say "Website pictures: $shotDir - look at them before you send any." 'Green'
    } else { Note 'Website pictures' 'skipped' }
}
foreach ($p in $restart) { if ($p -and (Test-Path $p)) { try { Start-Process -FilePath $p; Say "Started your AdeeboMeter again ($p)." } catch { } } }

# ---------------------------------------------------------------- the zip
$pass = @($results | Where-Object { $_.Result -eq 'PASS' }).Count; $fail = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count; $info = @($results | Where-Object { $_.Result -ne 'PASS' -and $_.Result -ne 'FAIL' }).Count
Say "Done: $pass passed, $fail failed, $info not tested or notes (see results.csv)." $(if ($fail) { 'Red' } elseif ($info) { 'Yellow' } else { 'Green' })   # hunters15 T-L5
$results | Export-Csv -Path (Join-Path $out 'results.csv') -NoTypeInformation -Encoding UTF8
$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) "AdeeboMeter-test-results-$stamp.zip"
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
Write-Host ''
Write-Host "Results: $zip" -ForegroundColor Green
Write-Host 'Send that zip back. (The test copy and its folder are in ' $work ' - safe to delete.)'
Read-Host 'Press Enter to close'
