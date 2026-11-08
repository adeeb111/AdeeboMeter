@echo off
rem Builds AdeeboMeter.exe from AdeeboMeter.cs with the C# compiler that ships inside Windows (.NET Framework 4.x).
rem Nothing to install, no administrator rights. Double-click it, or run it from a command prompt.
rem   build_exe.cmd            build, fingerprint, self-test
rem   build_exe.cmd notest     build and fingerprint only
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo The .NET Framework 4 compiler was not found in %WINDIR%\Microsoft.NET - it ships with Windows 10 and 11.
    goto fail
)
taskkill /im AdeeboMeter.exe >nul 2>nul
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:assets\AdeeboMeter.ico /out:AdeeboMeter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll AdeeboMeter.cs || goto fail
rem fingerprint: compare it with SHA256SUMS published with the release
certutil -hashfile AdeeboMeter.exe SHA256 | findstr /v ":" > AdeeboMeter.exe.sha256
echo Built AdeeboMeter.exe   SHA256
type AdeeboMeter.exe.sha256
if /i "%~1"=="notest" goto done
echo.
echo Running the self-test (a few seconds; small windows may flash) ...
start "" /wait AdeeboMeter.exe --selftest
findstr /b /c:"FAIL" /c:"ALL PASSED" /c:"FAILED" AdeeboMeter-selftest.txt
:done
echo.
echo Done. Double-click AdeeboMeter.exe - it offers to install itself, or runs from here.
pause
exit /b 0
:fail
echo Build failed - the reason is above.
pause
exit /b 1
