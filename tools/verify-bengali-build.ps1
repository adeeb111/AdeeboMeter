$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'dist/AdeeboMeter.exe'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static'
$taskLanguage = $taskAssembly.GetType('AdeeboMeter.L')
$taskLanguage.GetMethod('Use',$taskFlags).Invoke($null,@('bn')) | Out-Null
if($taskLanguage.GetMethod('T',$taskFlags).Invoke($null,@('Settings')) -ne 'সেটিংস'){throw 'Bengali lookup failed'}
$taskExpected = @([IO.File]::ReadAllLines((Join-Path $taskRoot 'knowledge/lang-bn.txt')) | Where-Object {$_ -and !$_.StartsWith('#') -and $_.Contains("`t")}).Count
if($taskLanguage.GetProperty('Count',$taskFlags).GetValue($null,$null) -ne $taskExpected){throw 'Embedded Bengali catalog differs from source'}
$taskDraw = $taskAssembly.GetType('AdeeboMeter.Draw')
$taskDraw.GetMethod('Init',$taskFlags).Invoke($null,@([single]1)) | Out-Null
$taskDraw.GetMethod('UseLanguageFonts',$taskFlags).Invoke($null,@()) | Out-Null
foreach($taskScale in @([single]1,[single]1.25,[single]1)){
 $taskDraw.GetMethod('Use',$taskFlags).Invoke($null,@($taskScale)) | Out-Null
 foreach($taskField in @('fBody','fHead','fTitle','fSmall')){
  $taskFont=$taskDraw.GetField($taskField,$taskFlags).GetValue($null)
  if($taskFont.Name -ne 'Nirmala UI'){throw "Bengali font lost at scale $taskScale : $($taskFont.Name)"}
 }
}
$taskAssembly.GetType('AdeeboMeter.Theme').GetMethod('Apply',$taskFlags).Invoke($null,@($true)) | Out-Null
$taskBitmap=New-Object Drawing.Bitmap 850,260
$taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
try{
 $taskGraphics.Clear([Drawing.Color]::FromArgb(10,20,36))
 $taskKit=$taskAssembly.GetType('AdeeboMeter.Kit')
 $taskKit.GetMethod('Header',$taskFlags).Invoke($null,@($taskGraphics,[single]24,[single]20,[single]800,'tools','Tools','All tools','toolbox')) | Out-Null
 $taskRect=New-Object Drawing.RectangleF 24,90,800,94
 $taskKit.GetMethod('Tile',$taskFlags).Invoke($null,@($taskGraphics,$taskRect,'network','The internet is slow','Check a destination','toolbox',$false,1)) | Out-Null
 $taskBitmap.Save((Join-Path (Split-Path $taskRoot -Parent) 'Bengali-Windows-text-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$taskGraphics.Dispose();$taskBitmap.Dispose()}
Write-Output "Passed embedded Bengali catalog ($taskExpected entries), lookup, Bengali font at 100%, 125%, 100%, native header/card rendering."
