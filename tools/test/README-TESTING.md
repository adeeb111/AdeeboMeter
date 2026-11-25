# AdeeboMeter tester run

1. Unzip everything into one folder (not Downloads if you can help it - Desktop is fine).
2. Right-click `Run-AdeeboMeterTests.ps1` › **Run with PowerShell**.
   If Windows blocks it: open PowerShell in that folder and run
   `powershell -ExecutionPolicy Bypass -File .\Run-AdeeboMeterTests.ps1`
3. If AdeeboMeter is running it asks to close it (it starts again at the end).
4. Step 3 moves your mouse and presses keys: hands off for about 3 minutes. Your windows are minimised and come back.
5. Step 4 asks whether to take 3 pictures for the website (Gamer, Streaming, Markets) with your real data. They go to
   `AdeeboMeter-website-pictures-<date>` on your Desktop, not into the zip - look at them before you send any.
6. Send back the zip it puts on your Desktop: `AdeeboMeter-test-results-<date>.zip`.

Nothing is installed. Steps 1-3 use test copies with their own data in a folder next to the script; step 4 only reads your history to draw the pictures.
Skip parts with `-SkipLive`, `-SkipSelfTest`, `-SkipGallery` or `-SkipShots`.
