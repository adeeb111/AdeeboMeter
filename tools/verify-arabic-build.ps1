$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$rootArabic = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$assemblyArabic = [Reflection.Assembly]::LoadFrom((Join-Path $rootArabic 'dist/AdeeboMeter.exe'))
$flagsArabic = [Reflection.BindingFlags]'Public,NonPublic,Static'
$languageArabic = $assemblyArabic.GetType('AdeeboMeter.L')
$languageArabic.GetMethod('Use',$flagsArabic).Invoke($null,@('ar')) | Out-Null
if($languageArabic.GetMethod('T',$flagsArabic).Invoke($null,@('Settings')) -ne 'الإعدادات'){throw 'Arabic lookup failed'}
$expectedArabic = @([IO.File]::ReadAllLines((Join-Path $rootArabic 'knowledge/lang-ar.txt')) | Where-Object {$_ -and !$_.StartsWith('#') -and $_.Contains("`t")}).Count
if($languageArabic.GetProperty('Count',$flagsArabic).GetValue($null,$null) -ne $expectedArabic){throw 'Embedded Arabic catalog differs from source'}
$drawArabic = $assemblyArabic.GetType('AdeeboMeter.Draw')
$drawArabic.GetMethod('Init',$flagsArabic).Invoke($null,@([single]1)) | Out-Null
$drawArabic.GetMethod('UseArabicFonts',$flagsArabic).Invoke($null,@()) | Out-Null
foreach($scaleArabic in @([single]1,[single]1.25,[single]1)){
 $drawArabic.GetMethod('Use',$flagsArabic).Invoke($null,@($scaleArabic)) | Out-Null
 foreach($fieldArabic in @('fBody','fHead','fTitle','fSmall')){if($drawArabic.GetField($fieldArabic,$flagsArabic).GetValue($null).Name -ne 'Segoe UI'){throw "Arabic font lost at scale $scaleArabic"}}
}
$themeArabic=$assemblyArabic.GetType('AdeeboMeter.Theme')
$themeArabic.GetMethod('Apply',$flagsArabic).Invoke($null,@($true)) | Out-Null
$bitmapArabic=New-Object Drawing.Bitmap 850,260
$graphicsArabic=[Drawing.Graphics]::FromImage($bitmapArabic)
try{
 $graphicsArabic.Clear([Drawing.Color]::FromArgb(10,20,36))
 $kitArabic=$assemblyArabic.GetType('AdeeboMeter.Kit')
 $kitArabic.GetMethod('Header',$flagsArabic).Invoke($null,@($graphicsArabic,[single]24,[single]20,[single]800,'tools','Tools','All tools','toolbox')) | Out-Null
 $rectArabic=New-Object Drawing.RectangleF 24,90,800,94
 $kitArabic.GetMethod('Tile',$flagsArabic).Invoke($null,@($graphicsArabic,$rectArabic,'network','The internet is slow','Check a destination','toolbox',$false,1)) | Out-Null
 $imageArabic=Join-Path (Split-Path $rootArabic -Parent) 'Arabic-Windows-text-preview.png'
 $bitmapArabic.Save($imageArabic,[Drawing.Imaging.ImageFormat]::Png)
}finally{$graphicsArabic.Dispose();$bitmapArabic.Dispose()}
$languageArabic.GetMethod('Use',$flagsArabic).Invoke($null,@('hi')) | Out-Null
if($languageArabic.GetField('Code',$flagsArabic).GetValue($null) -ne 'en'){throw 'Removed Hindi language did not fall back'}
Write-Output "Passed embedded Arabic catalog ($expectedArabic entries), Arabic font zoom/restoration, RTL card rendering, Hindi fallback."
