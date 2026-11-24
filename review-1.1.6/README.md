# AdeeboMeter 1.1.6 review (commit 999a100, branch dev-1.1.6)

Independent release review. Findings only: nothing in the product was changed, and nothing was pushed to dev-1.1.6.

**Start here: [RELEASE-VERDICT.md](RELEASE-VERDICT.md)**. It has the 4 blockers, what to fix before release, the totals, what held up, and the questions for you.

## Reports

| File | Covers | Findings |
|---|---|---|
| RELEASE-VERDICT.md | Merged verdict | 4 blockers |
| R5-security.md | Phone remote (Remote/*, Relay, docs/app.html), AdeeboShell + editor, LabLink, bug report, Weather, rule 1/2/8 grep | 11 |
| R5-fixcheck.md | All 137 round-4 findings re-checked (75 re-run), memory investigation, release gates (version, .gitignore, CI key, docs counts) | 126 fixed, 9 partial, 2 still present, 4 new |
| D1-day-one.md | Day-1 journeys run with real clicks: first run from Downloads, portable, read-only folder, 5 languages, upgrade from 1.1.5, first click on every page and all 232 tools, offline remote/Shell/Markets/Sports, uninstall | 17 |
| P1-core.md | src/*.cs (core), Apps, Brain, Knowledge, every language and buddy-line table | 16 |
| P2-checks1.md | src/Checks Calc .. OverTime | 15 |
| P3-checks2.md | src/Checks PathTools .. WinEvidence | 21 |
| P4-watch-link-deep.md | src/Watch (Markets, Sports, Streaming, broker), Link, Capture, WindowsDeep | 11 |
| P5-gamer-labs-ci-docs.md | Gamer, Labs, SelfTest, CI, release scripts, website (555 pages; 23 in Playwright) | 13 |
| U1-ui.md … U4-ui.md | All 101 UI files, about 7,000 lines each | 19 / 11 / 12 / 13 |

Every finding uses the owner's format (title, Severity, Where, How to reproduce, What happens / should happen, Confidence, Rule). "[day 1]" marks first-run bugs.

## How it was tested

- **Build.** 999a100 was compiled with mono `mcs -langversion:5` with the knowledge, font, emoji and lab.html resources embedded, the same way `build_exe.cmd` embeds them. Mono lacks three Windows-only APIs, so these were adjusted for the Linux build only:
  - `harnesses/build-stubs/zz_stub.cs` stubs `ECDiffieHellmanCng`, so the P-256/P-384 handshake paths were reviewed by reading.
  - `zz_stub2.cs` stubs `EventLogReader`.
  - In `HwSpecs.cs`, `EnumerationOptions` was qualified as `System.Management.EnumerationOptions`.
- **What "confirmed" means.** The finding was run: by reflection on the build, against fake local servers (iperf3, SMTP, NTP, DNS, TLS with generated certificates, SNMP agent), by fuzzing, with Playwright on a local copy of docs/, or, for day-1, by real X clicks on a mono-runnable build.
- **What "suspected" means.** Read only. Usually the behaviour depends on Windows (Run key, WMI, NTLM, real fonts, .NET Framework specifics).
- **Mono artifacts.** These are not counted as bugs: 26–27 self-test failures from Windows-only calls and backslash paths, and the 2,046 MB memory reading. That reading comes from libgdiplus mis-measuring the embedded Barlow font; see R5-fixcheck.md.
- **Not done.** Nothing was run on Windows. Build 1124o on your PC wasn't reachable, and the list of 49 findings you fixed wasn't in the repo.

## Re-running a harness

The harness sources are in `harnesses/<report>/`:
- `*.cs` files are mono harnesses.
- `*.py` files are fake servers and checkers.
- `*.sh` files are the day-1 scripts.

To run one:
1. Build the code as described above.
2. Compile the harness with `mcs -r:r6.dll <file>.cs`. Most call internal classes by reflection.
3. Run it with `mono <file>.exe`.

The day-1 journey also needs Xvfb, plus `D1-day-one/shim.c` built as `libshim.so` and mapped for the Windows DLLs, and the fonts.conf from that folder.

No real Hall of Fame key or API key was used. The Hall of Fame tests used a dummy key on a scratch copy of the repo.

## Earlier rounds

- `../review-1.1.5/claude-code.md`: round 1 (f6d5747), 244 findings.
- `round-2/`: a0fbd56.
- `round-3/`: 8502674.
- `round-4/`: e80412e, 137 findings, the deep inspection.
