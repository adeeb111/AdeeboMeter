# Security

AdeeboMeter is built to be safe to run on a work PC: no administrator rights, no listening ports, no cloud, no shell.
How it's built is in [the Theory of Everything, section 7](docs/THEORY-OF-EVERYTHING.md#7-security-in-short).

## Supported versions

Only the latest release gets fixes. Update by running the new `AdeeboMeter.exe` and choosing **Update**.

## Reporting a vulnerability

Please **don't open a public issue** for a security problem. Use GitHub's private report instead:
**[Report a vulnerability](https://github.com/adeeb111/AdeeboMeter/security/advisories/new)**.

Say what you found, how to reproduce it, and which version (tray icon → first line of the menu). You'll get an answer
within a week; a fix ships as a new release with credit to you, unless you'd rather stay anonymous.

## Checking a download

Every release lists the SHA256 of each file, built by GitHub Actions from the public source. In PowerShell:

```powershell
Get-FileHash .\AdeeboMeter.exe
```
