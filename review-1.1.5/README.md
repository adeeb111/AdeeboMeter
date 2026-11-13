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

**Start here: `claude-code.md`**. A second reviewer's report is in `muse.md`; claude-code.md ends with the cross-check of it.

**claude-code.md**. It has all 228 findings in the brief's format, with dev-1.1.5 line numbers, rule breaks and the top 15 to fix. The other files are the detailed section reports it was built from.

## Found so far directly on the 1.1.5 test build (ran it under mono)
- Version still says 1.1.4 (`src/Program.cs:66`): `--report` writes `"app": "AdeeboMeter 1.1.4"`, and tagging v1.1.5 fails the release workflow's version check.
- Rule 8: Nokia reference, "On SR OS: ..." (`src/Checks/Protocols.cs:418`).
- `--report` can say "names ... all work" while `measured_ms.dns` is null (the monitor's 30 s DNS probe may not run inside a 15 s report).

## Round 2 (a0fbd56)

**Start here: `round-2/claude-code.md`**. It covers the fix status of all 244 round-1 findings, 131 findings (plus #81, withdrawn), the cost table, the top 15, and where Muse is wrong.
Section files: `round-2/R2-fixcheck.md`, `R2-calc-labs.md`, `R2-hof-points-update.md`, `R2-core-tools.md`, `R2-perf.md`. Muse's round-2 reports: `round-2/muse.md`, `round-2/muse-200-bugs.md`; the check of Muse's Mediums goes in `round-2/muse-200-check.md`.

## Round 4 - deep inspection (e80412e)

**Start here: `round-4/claude-code.md`**. It has 137 findings (merged from 151 across 8 areas), the top 20, rule breaks, what's still open from earlier rounds, and what held up.
Area reports: `round-4/S1-core.md` ... `S8-ui-rest-ci-docs.md`.
