# Contributing

Thanks for wanting to make AdeeboMeter better.

- **Ideas and questions** → [Discussions](https://github.com/adeeb111/AdeeboMeter/discussions) (or the suggestion box on the [website](https://adeeb111.github.io/AdeeboMeter/#feedback)).
- **Something broken** → [Report a problem](https://github.com/adeeb111/AdeeboMeter/issues/new/choose). The self-test output helps a lot.
- **Security** → privately, see [SECURITY.md](SECURITY.md).

## Changing the code

Everything is one file, `AdeeboMeter.cs`, built by the C# compiler that ships with Windows — double-click
`build_exe.cmd`. It builds, prints the SHA256 and runs the self-test (`AdeeboMeter.exe --selftest`).

A change is welcome when it keeps the promises:

1. **Offline** — no cloud, no telemetry, no update check, never a listening port. Anything that sends a packet is listed in *About* and can be switched off.
2. **No administrator rights** — if Windows says no, that part simply doesn't show.
3. **Penny-light** — heavy work only while the user is looking, and it stops by itself.
4. **Safe** — no shell; Windows tools by full System32 path; inputs validated; TLS 1.2+ with Windows' certificate checks.
5. **One readable file, C# 5** — it must build with Windows' own `csc.exe` (.NET Framework 4.x).
6. **Plain English** — every new label gets an explanation for the **?** hover card.

Add a line to `CHANGELOG.md` under the next version, and keep the self-test passing.

## Releasing (maintainer)

Change `Program.Version`, add a `## <version> (date)` section to `CHANGELOG.md`, push, then publish a GitHub release
tagged `v<version>`. GitHub Actions builds and attaches everything and writes the release notes.
