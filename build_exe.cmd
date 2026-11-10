@echo off
rem Builds AdeeboMeter.exe from the source in src\ with the C# compiler that ships inside Windows (.NET Framework 4.x).
rem Nothing to install, no administrator rights. Double-click it, or run it from a command prompt.
rem   build_exe.cmd            build, fingerprint, self-test
rem   build_exe.cmd notest     build and fingerprint only
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo The .NET Framework 4 compiler was not found in %WINDIR%\Microsoft.NET - it ships with Windows 10 and 11.
    goto fail
)
taskkill /im AdeeboMeter.exe >nul 2>nul
rem the knowledge (knowledge\*.txt) rides compressed inside the exe and is unpacked only when needed
if not exist obj mkdir obj
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem knowledge\*.txt | ForEach-Object { $b=[IO.File]::ReadAllBytes($_.FullName); $o=[IO.File]::Create('obj\'+$_.BaseName+'.gz'); $z=New-Object IO.Compression.GZipStream($o,[IO.Compression.CompressionLevel]::Optimal); $z.Write($b,0,$b.Length); $z.Close(); $o.Close() }" || goto fail
set "RES="
for %%F in (knowledge\*.txt) do set "RES=!RES! /resource:obj\%%~nF.gz,AdeeboMeter.knowledge.%%~nF.gz"
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:assets\AdeeboMeter.ico /out:AdeeboMeter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Management.dll /recurse:src\*.cs !RES! || goto fail
rem fingerprint: compare it with SHA256SUMS published with the release
certutil -hashfile AdeeboMeter.exe SHA256 | findstr /v ":" > AdeeboMeter.exe.sha256
echo Built AdeeboMeter.exe   SHA256
type AdeeboMeter.exe.sha256
if /i "%~1"=="notest" goto done
echo.
echo Running the self-test (a few seconds; small windows may flash) ...
start "" /wait AdeeboMeter.exe --selftest
set "RC=!errorlevel!"
findstr /b /c:"FAIL" /c:"ALL PASSED" /c:"FAILED" AdeeboMeter-selftest.txt
if not "!RC!"=="0" (
    echo Self-test FAILED - exit code !RC!. Don't release this build.
    pause
    exit /b !RC!
)
:done
echo.
echo Done. Double-click AdeeboMeter.exe - it offers to install itself, or runs from here.
pause
exit /b 0
:fail
echo Build failed - the reason is above.
pause
exit /b 1
