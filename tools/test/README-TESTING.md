# AdeeboMeter tester run

1. Unzip everything into one folder (not Downloads if you can help it - Desktop is fine).
2. Right-click `Run-AdeeboMeterTests.ps1` › **Run with PowerShell**.
   If Windows blocks it: open PowerShell in that folder and run
   `powershell -ExecutionPolicy Bypass -File .\Run-AdeeboMeterTests.ps1`
3. If AdeeboMeter is running it asks to close it (it starts again at the end).
4. Step 3 moves your mouse and presses keys: hands off for about 3 minutes. Your windows are minimised and come back.
5. Send back the zip it puts on your Desktop: `AdeeboMeter-test-results-<date>.zip`.

Nothing is installed and your own settings and history aren't touched: the test copies live in a temporary folder.
Only the self-test and the widget pictures: add `-SkipLive`.
