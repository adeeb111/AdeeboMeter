$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'dist/AdeeboMeter.exe'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Static'
$taskLanguage = $taskAssembly.GetType('AdeeboMeter.L')
$taskLanguage.GetMethod('Use',$taskFlags).Invoke($null,@('es')) | Out-Null
if($taskLanguage.GetProperty('IsRtl',$taskFlags).GetValue($null,$null)){throw 'Spanish must remain LTR'}
$taskExpected=0
foreach($taskLine in [IO.File]::ReadAllLines((Join-Path $taskRoot 'knowledge/lang-es.txt'))){
 if(!$taskLine -or $taskLine.StartsWith('#') -or !$taskLine.Contains("`t")){continue}
 $taskParts=$taskLine.Split(@([char]9),2)
 if($taskLanguage.GetMethod('T',$taskFlags).Invoke($null,@($taskParts[0])) -cne $taskParts[1]){throw "Embedded translation mismatch: $($taskParts[0])"}
 $taskExpected++
}
if($taskLanguage.GetProperty('Count',$taskFlags).GetValue($null,$null) -ne $taskExpected){throw 'Embedded Spanish count differs from source'}
if($taskLanguage.GetMethod('T',$taskFlags).Invoke($null,@('Settings')) -ne 'Ajustes'){throw 'Spanish lookup failed'}
if($taskLanguage.GetMethod('T',$taskFlags).Invoke($null,@('not in the table at all')) -ne 'not in the table at all'){throw 'Fallback sentinel translated'}
$taskDraw=$taskAssembly.GetType('AdeeboMeter.Draw')
$taskDraw.GetMethod('Init',$taskFlags).Invoke($null,@([single]1)) | Out-Null
$taskDraw.GetMethod('UseLanguageFonts',$taskFlags).Invoke($null,@()) | Out-Null
$taskFonts=@{}
foreach($taskField in @('fBody','fHead','fTitle','fSmall')){$taskFonts[$taskField]=$taskDraw.GetField($taskField,$taskFlags).GetValue($null).Name}
foreach($taskScale in @([single]1,[single]1.25,[single]1)){
 $taskDraw.GetMethod('Use',$taskFlags).Invoke($null,@($taskScale)) | Out-Null
 foreach($taskField in @('fBody','fHead','fTitle','fSmall')){
  $taskFont=$taskDraw.GetField($taskField,$taskFlags).GetValue($null)
  # The embedded small face may become available after the initial Windows fallback.
  $taskAllowed=@($taskFonts[$taskField],$taskDraw.GetField('UiFam',$taskFlags).GetValue($null),$taskDraw.GetField('UiSemi',$taskFlags).GetValue($null),$taskDraw.GetField('SmallFam',$taskFlags).GetValue($null))
  if($taskFont.Name -notin $taskAllowed -or $taskFont.Size -le 0){throw "Unexpected Spanish font at $taskScale : $taskField $($taskFont.Name)"}
  if($taskScale -eq 1 -and $taskFont.Name -ne $taskFonts[$taskField]){throw 'Base font not restored after zoom'}
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
 $taskBitmap.Save((Join-Path (Split-Path $taskRoot -Parent) 'Spanish-Windows-text-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$taskGraphics.Dispose();$taskBitmap.Dispose()}
Write-Output "Passed all $taskExpected embedded Spanish pairs, LTR, fallback, font zoom 100%-125%-100%, native header/card render."
