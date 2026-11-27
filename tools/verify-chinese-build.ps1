$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskAsm=[Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'dist/AdeeboMeter.exe'))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static'
$lang=$taskAsm.GetType('AdeeboMeter.L')
$lang.GetMethod('Use',$flags).Invoke($null,@('zh')) | Out-Null
if($lang.GetField('Code',$flags).GetValue($null) -ne 'zh'){throw 'Chinese was not loaded'}
if($lang.GetProperty('IsRtl',$flags).GetValue($null,$null)){throw 'Chinese must be LTR'}
$expected=0
foreach($line in [IO.File]::ReadAllLines((Join-Path $taskRoot 'knowledge/lang-zh.txt'))){
 if(!$line -or $line.StartsWith('#')){continue};$tab=$line.IndexOf([char]9)
 if($tab -le 0){throw 'Malformed catalog row'}
 $key=$line.Substring(0,$tab);$value=$line.Substring($tab+1)
 if(!$value.Trim() -or $lang.GetMethod('T',$flags).Invoke($null,@($key)) -cne $value){throw "Embedded mismatch: $key"};$expected++
}
if($lang.GetProperty('Count',$flags).GetValue($null,$null) -ne $expected){throw 'Embedded count mismatch'}
$lines=$taskAsm.GetType('AdeeboMeter.Lines');$packs=0
$lines.GetField('Addr',$flags).SetValue($null,'neutral')
foreach($persona in @('plain','bro','pirate','epic-fantasy','gamer','mission')){
 $address=$lines.GetMethod('AddrFor',$flags).Invoke($null,@($persona))
 if($address -notmatch '[\u3400-\u9fff]'){throw "Chinese address missing for $persona"}
}
foreach($pack in Get-ChildItem (Join-Path $taskRoot 'knowledge') -Filter 'lines-*-zh.txt'){
 $id=$pack.BaseName.Substring(6);$id=$id.Substring(0,$id.Length-3)
 $errors=@($lines.GetMethod('Check',$flags).Invoke($null,@($id,[IO.File]::ReadAllText($pack.FullName))))
 if($errors.Count -gt 0){throw ($errors -join '; ')};$packs++
}
$draw=$taskAsm.GetType('AdeeboMeter.Draw')
$draw.GetMethod('Init',$flags).Invoke($null,@([single]1)) | Out-Null
$draw.GetMethod('UseLanguageFonts',$flags).Invoke($null,@()) | Out-Null
$family=$draw.GetProperty('ChineseFontFamily',$flags).GetValue($null,$null)
foreach($scale in @([single]1,[single]1.25,[single]1)){
 $draw.GetMethod('Use',$flags).Invoke($null,@($scale)) | Out-Null
 foreach($name in @('fBody','fHead','fTitle','fSmall')){
  if($draw.GetField($name,$flags).GetValue($null).Name -ne $family){throw "Chinese font lost at scale $scale"}
 }
}
$taskAsm.GetType('AdeeboMeter.Theme').GetMethod('Apply',$flags).Invoke($null,@($true)) | Out-Null
$bitmap=New-Object Drawing.Bitmap 900,260;$g=[Drawing.Graphics]::FromImage($bitmap)
try{
 $g.Clear([Drawing.Color]::FromArgb(10,20,36));$kit=$taskAsm.GetType('AdeeboMeter.Kit')
 $kit.GetMethod('Header',$flags).Invoke($null,@($g,[single]24,[single]20,[single]850,'tools','Tools','All tools','toolbox')) | Out-Null
 $rect=New-Object Drawing.RectangleF 24,90,850,100
 $kit.GetMethod('Tile',$flags).Invoke($null,@($g,$rect,'network','The internet is slow','Check a destination','toolbox',$false,1)) | Out-Null
 $bitmap.Save((Join-Path (Split-Path $taskRoot -Parent) 'Chinese-Windows-text-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$g.Dispose();$bitmap.Dispose()}
Write-Output "Passed $expected embedded Chinese entries, $packs persona packs, LTR, $family at 100/125/100 percent and native rendering."
