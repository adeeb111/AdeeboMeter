<#
  AdeeboMeter tour for screen recordings (OBS Studio) - for the content creators.

  The app walks every Toolbox tool by itself: opens it, fills its Example inputs, runs it (or shows its example when
  the tool sends test traffic, touches files or needs a password), scrolls the result to the end, holds a few seconds,
  then the next tool. It writes tour-chapters.csv: when each tool started, so one recording becomes a clip per tool.

  1) RECORD (about an hour for all 232 tools):
       powershell -ExecutionPolicy Bypass -File Record-Tour.ps1 -Exe "C:\path\AdeeboMeter.exe"
       powershell -ExecutionPolicy Bypass -File Record-Tour.ps1 -Exe ... -From mtr -Count 1    (redo one tool)
       powershell -ExecutionPolicy Bypass -File Record-Tour.ps1 -Exe ... -Hold 8               (8 s on each result)
     The script asks you to start OBS first, then starts the tour. Don't touch the mouse or keyboard while it runs;
     Esc stops the tour. At the end it copies tour-chapters.csv into a folder on your Desktop.

  2) CUT (needs ffmpeg: winget install Gyan.FFmpeg):
       powershell -ExecutionPolicy Bypass -File Record-Tour.ps1 -Split "C:\Videos\2026-11-28 10-00-05.mkv" -Chapters "...\tour-chapters.csv"
     Makes one MP4 per tool (001-ping.mp4, 002-mtr.mp4 ...) and youtube-chapters.txt (timestamps for the combined
     demo's description). The recording's start time is read from OBS's file name (yyyy-MM-dd HH-mm-ss); if you renamed
     the file, add -RecordingStart "2026-11-28 10:00:05".

  Nothing here is sent anywhere. ASCII only on purpose (Windows PowerShell reads a script without a BOM as ANSI).
#>
param(
    [string]$Exe = "",
    [string]$From = "",
    [int]$Hold = 5,
    [int]$Count = 0,
    [string]$Split = "",
    [string]$Chapters = "",
    [string]$RecordingStart = "",
    [string]$Out = ""
)
$ErrorActionPreference = "Stop"
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function Say([string]$s) { Write-Host $s }

# ------------------------------------------------------------------ 2) cut a recording into one clip per tool
if ($Split -ne "") {
    if (-not (Test-Path $Split)) { Say "Recording not found: $Split"; exit 2 }
    if ($Chapters -eq "" -or -not (Test-Path $Chapters)) { Say "Give -Chapters with the tour-chapters.csv from the same run."; exit 2 }
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { Say "ffmpeg isn't installed: winget install Gyan.FFmpeg, then open a new PowerShell window."; exit 2 }
    $rec = Get-Item $Split
    if ($RecordingStart -ne "") { $t0 = [datetime]::Parse($RecordingStart, $inv) }
    else {
        $m = [regex]::Match($rec.BaseName, '\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}')
        if ($m.Success) { $t0 = [datetime]::ParseExact($m.Value, 'yyyy-MM-dd HH-mm-ss', $inv) }
        else { $t0 = $rec.CreationTime; Say "No OBS time in the file name - using the file's creation time ($t0). Use -RecordingStart if that's wrong." }
    }
    $rows = @(Import-Csv $Chapters)
    if ($rows.Count -eq 0) { Say "The chapters file is empty."; exit 2 }
    if ($Out -eq "") { $Out = Join-Path $rec.DirectoryName ($rec.BaseName + " - clips") }
    New-Item -ItemType Directory -Force $Out | Out-Null
    $yt = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $r = $rows[$i]
        $s = ([datetime]::ParseExact($r.clock, 'yyyy-MM-dd HH:mm:ss.fff', $inv) - $t0).TotalSeconds
        if ($s -lt 0) { Say ("Skipping " + $r.tool + ": it started before the recording did."); continue }
        $dur = $null
        if ($i + 1 -lt $rows.Count) { $dur = ([datetime]::ParseExact($rows[$i + 1].clock, 'yyyy-MM-dd HH:mm:ss.fff', $inv) - $t0).TotalSeconds - $s }
        $name = ('{0:000}-{1}.mp4' -f [int]$r.n, ($r.tool -replace '[^A-Za-z0-9_-]', '_'))
        $fa = @('-hide_banner', '-loglevel', 'error', '-y', '-ss', $s.ToString('0.###', $inv), '-i', $rec.FullName)
        if ($dur -ne $null) { $fa += @('-t', $dur.ToString('0.###', $inv)) }
        $fa += @('-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-c:a', 'aac', (Join-Path $Out $name))
        Say ("[{0}/{1}] {2}  {3}" -f ($i + 1), $rows.Count, $name, $r.title)
        & ffmpeg @fa
        if ($LASTEXITCODE -ne 0) { Say "  ffmpeg couldn't cut this one - the rest go on." }
        $ts = [TimeSpan]::FromSeconds([math]::Floor($s))
        [void]$yt.AppendLine(('{0}:{1:00}:{2:00} {3}' -f [int][math]::Floor($ts.TotalHours), $ts.Minutes, $ts.Seconds, $r.title))
    }
    [System.IO.File]::WriteAllText((Join-Path $Out 'youtube-chapters.txt'), $yt.ToString())
    Say ""; Say "Done: the clips and youtube-chapters.txt are in $Out"
    exit 0
}

# ------------------------------------------------------------------ 1) record: start the tour
if ($Exe -eq "") { $Exe = Join-Path (Get-Location) 'AdeeboMeter.exe' }
if (-not (Test-Path $Exe)) { Say "AdeeboMeter.exe not found. Give its path: -Exe ""C:\...\AdeeboMeter.exe"""; exit 2 }
$Exe = (Resolve-Path $Exe).Path
$dirs = @((Split-Path $Exe), (Join-Path $env:LOCALAPPDATA 'AdeeboMeter'))   # where the app writes: beside the exe, or its own folder

$running = @(Get-Process -Name AdeeboMeter -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $a = Read-Host "AdeeboMeter is running. Close it for the recording? (y/n) - afterwards, close the tour copy and start yours again"
    if ($a -notmatch '^[yY]') { Say "Stopped - close AdeeboMeter first."; exit 1 }
    $running | ForEach-Object { try { $_.CloseMainWindow() | Out-Null } catch {} }
    Start-Sleep 3
    Get-Process -Name AdeeboMeter -ErrorAction SilentlyContinue | Stop-Process -Force
}
foreach ($d in $dirs) { $f = Join-Path $d 'tour-chapters.csv'; if (Test-Path $f) { Remove-Item $f -Force } }

Say ""
Say "1. In OBS: Window Capture (or Display Capture) of the screen AdeeboMeter will open on."
Say "2. Start recording in OBS."
Read-Host "3. Then press Enter here - the tour starts a few seconds later"

$view = if ($From -ne "") { "tour:$From" } else { "tour" }
$al = @('--view', $view, '--hold', [string]$Hold)
if ($Count -gt 0) { $al += @('--count', [string]$Count) }
$p = Start-Process -FilePath $Exe -ArgumentList $al -PassThru
Say "Touring... hands off the mouse and keyboard. Esc stops it."

$sw = [Diagnostics.Stopwatch]::StartNew(); $got = $null
while ($sw.Elapsed.TotalHours -lt 3 -and -not $got) {
    Start-Sleep 5
    foreach ($d in $dirs) { $f = Join-Path $d 'tour-chapters.csv'; if (Test-Path $f) { $got = $f } }
    if ($p.HasExited -and -not $got) { break }
}
if (-not $got) { Say "The tour didn't write its chapters (closed early?). Nothing to copy."; exit 1 }
Start-Sleep 2
if ($Out -eq "") { $Out = Join-Path ([Environment]::GetFolderPath('Desktop')) ('AdeeboMeter-tour-' + (Get-Date -Format 'yyyy-MM-dd_HH-mm')) }
New-Item -ItemType Directory -Force $Out | Out-Null
Copy-Item $got (Join-Path $Out 'tour-chapters.csv') -Force
$n = @(Import-Csv $got).Count
Say ""
Say "Tour done: $n tools. Stop the OBS recording now."
Say "Chapters: $(Join-Path $Out 'tour-chapters.csv')"
Say "To cut it into one clip per tool:"
Say "  powershell -ExecutionPolicy Bypass -File Record-Tour.ps1 -Split ""<your OBS recording>"" -Chapters ""$(Join-Path $Out 'tour-chapters.csv')"""
