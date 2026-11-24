
## New bugs

### N1. Turkish Windows: "Your connection" shows your own Wi-Fi signal as 0% in red
Severity: wrong result  
Where: src\Link\LinkInfo.cs line 648 (Collect: `if (f.TryGetValue("Signal", out v)) int.TryParse(v.TrimEnd('%', …), …, out sig);`), with line 519 (the new `{ "sinyal", "Signal" }` KeyWords entry from the #35 fix) and line 651 (`if (sig < 0) sig = psig;`)  
How to reproduce: feed `netsh wlan show interfaces` output from Turkish Windows, which writes the percent sign first (`Sinyal : %88`). Sample: review5/fix/h/hS2/netsh/tr2.if.txt, run with `mono N.exe tr2`.  
What happens / what should happen: the Signal row reads `0%`, level 2 (red), while the access-point list beside it shows 88%. `TrimEnd('%')` leaves "%88", so `TryParse` fails and sets `sig` to 0, not -1. Because 0 is not below 0, the `Monitor.ParseWifi` fallback (which reads the number by its shape) is skipped. In round 4 "Sinyal" wasn't mapped, so the fallback ran and gave 88%. This is a regression from the #35 fix. The fix is to trim '%' on both sides (or take the digits with a regex), and to keep -1 when the parse fails.  
Confidence: confirmed (ran it: hS2/N.exe tr2 → sig=0, using the same LinkInfo.Fields and the same parse as line 648). That Turkish netsh puts % first is suspected; it is Turkish number formatting, and the sample was not captured on a real PC.

### N2. Rule 8: the forbidden vendor and its router OS are named in two tracked files that ship in the source zip
Severity: wrong result  
Where: HANDOVER-SEO.md line 7 ("**No company/employer references anywhere** (no N…, no employers' product names)"); PLAN-1.1.5.md line 558 ("…any import and SR OS alignment lives in topogram."); release.ps1 line 85 (`git archive … HEAD`)  
How to reproduce: `git grep -n -i <vendor>` and `git grep -n 'SR OS'` at 999a100.  
What happens / what should happen: the release's source zip and the public repository contain the name. Round 4 #33's line 8 was reworded, but HANDOVER-SEO.md (added 17 Nov) repeats the same rule with the name spelled out. Reword both ("the forbidden vendor (see the private brief)", "the vendor's router OS"), or keep handover and plan files out of the repository. A `git grep` step for these words in tools/ci_checks.py would stop it happening again.  
Confidence: confirmed (git grep)  
Rule: 8

### N3. The self-test count is wrong in README, on the website and in the Theory of Everything
Severity: cosmetic  
Where: README.md line 86 and docs/index.html line 1030 ("93 checks"); docs/THEORY-OF-EVERYTHING.md line 281 ("104 checks"); src/SelfTest.cs (100 T() checks, 101 with `--samples`)  
How to reproduce: run `AdeeboMeter.exe --selftest` and count the PASS and FAIL lines: 100.  
What happens / what should happen: three different numbers are published, and none matches the code. Have the self-test print its own count, and check it against README in tools/ci_checks.py, as round 4 #114 suggested for the other numbers.  
Confidence: confirmed (ran --selftest under mono; grep)

### N4. Widget renderers leak their bitmap when drawing throws, and size the card from text with no upper bound
Severity: slow  
Where: src\Ui\Theme.cs lines 680, 735, 801, 852 (GamerCard, CallsCard, EngineerCard, Card); src\Ui\SpeedWidget.cs lines 79 and 120. In each, `var bmp = new Bitmap(W, H)` is followed by `using (var g = PrepW(bmp)) {…}` with no try/dispose for `bmp`, and `H` comes from `Measure(headline/quip/hint)` with no cap.  
How to reproduce: under mono, review5/fix/mem/pv.exe calls `Draw.Preview` for every style. libgdiplus returns heights of about 1.4 M px for the embedded Barlow font, so "speedstatus", "speedapps" and "calls" each add 480-1,400 MB of RSS, throw in `Graphics.Clear`, and the memory is never returned (RSS about 2 GB after GC). The self-test's "memory after everything" then reads 2,046 MB.  
What happens / what should happen: on Windows the metrics are sane, so the height stays small. But any exception while drawing a card (GDI+ "Parameter is not valid" from a disposed font happened in 1.1.6, per commit 58a7f4e) leaks one card bitmap per draw, and the widget redraws every 1-5 s. Wrap the drawing in try { … } catch { bmp.Dispose(); throw; }, and clamp `H` (for example to 3× the normal card height).  
Confidence: confirmed on mono (ran it). The Windows leak is suspected (read).
