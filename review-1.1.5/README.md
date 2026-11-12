# AdeeboMeter 1.1.5 review

Independent review. Findings only; nothing in the product was changed.

| Part | Code | Status | File |
|---|---|---|---|
| Website & docs | main (1.1.4) | done | sections-1.1.4/01-website.md |
| Core, startup, setup, monitor, brain, release | main (1.1.4) | done | sections-1.1.4/02-core.md |
| Input tools, Traffic Lab, security checks | main (1.1.4) | done | sections-1.1.4/03-tools-input.md |
| SNMP, routing, investigate | main (1.1.4) | done | sections-1.1.4/04-snmp-investigate.md |
| UI | main (1.1.4) | done | sections-1.1.4/05-ui.md |
| Capture, Wi-Fi, Windows deep, native | main (1.1.4) | done | sections-1.1.4/06-capture-link-deep.md |
| Which 1.1.4 findings are still on dev-1.1.5 | dev-1.1.5 | done: 167 of 169 still present, 2 partly fixed, 0 fixed | 10-carryover.md |
| Crypto, keys, policy, report, portable, Store | dev-1.1.5 | done: 2 High, 7 Medium, rest Low | 07-crypto-policy-store.md |
| Engineer pack, pcap, replays, speed test, ISP report, Gamer | dev-1.1.5 | done: 14 confirmed, 11 suspected | 08-engpack-gamer.md |
| New UI: Examples, Points, Toolbox home, French | dev-1.1.5 | done: 12 confirmed, 6 suspected | 09-ui-115.md |

**Start here: `claude-code.md`**. It has all 228 findings in the brief's format, with dev-1.1.5 line numbers, rule breaks and the top 15 to fix. The other files are the detailed section reports it was built from.

## Found so far directly on the 1.1.5 test build (ran it under mono)
- Version still says 1.1.4 (`src/Program.cs:66`): `--report` writes `"app": "AdeeboMeter 1.1.4"`, and tagging v1.1.5 fails the release workflow's version check.
- Rule 8: Nokia reference, "On SR OS: ..." (`src/Checks/Protocols.cs:418`).
- `--report` can say "names ... all work" while `measured_ms.dns` is null (the monitor's 30 s DNS probe may not run inside a 15 s report).
