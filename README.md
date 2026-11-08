<p align="center"><img src="docs/favicon.svg" width="88" alt="AdeeboMeter"></p>
<h1 align="center">AdeeboMeter</h1>

<p align="center">
<a href="https://github.com/adeeb111/AdeeboMeter/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/adeeb111/AdeeboMeter?label=download&color=1F6FEB"></a>
<a href="https://github.com/adeeb111/AdeeboMeter/actions/workflows/build.yml"><img alt="Build" src="https://github.com/adeeb111/AdeeboMeter/actions/workflows/build.yml/badge.svg"></a>
<img alt="Windows 10 and 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
<img alt="No admin" src="https://img.shields.io/badge/admin%20rights-none-2DA44E">
<img alt="Offline" src="https://img.shields.io/badge/telemetry-none-2DA44E">
<a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-8250DF"></a>
</p>

<p align="center"><b><a href="https://adeeb111.github.io/AdeeboMeter/">Website</a></b> · <a href="https://github.com/adeeb111/AdeeboMeter/releases/latest">Download</a> · <a href="https://adeeb111.github.io/AdeeboMeter/#theory">Theory of Everything</a> · <a href="https://github.com/adeeb111/AdeeboMeter/discussions">Discussions</a></p>

<p align="center"><img src="docs/img/screenshot.png" alt="AdeeboMeter: the engineer view and the corner widget on a Windows desktop" width="860"></p>

**Is it me or the network?** One glance tells you.

AdeeboMeter is a small Windows companion for people who live on the network. A faded card in the corner of your screen tells you — in one plain sentence — whether your PC, your Wi-Fi, your provider, DNS or the internet is the problem, and what to do about it. When something is really wrong, one click opens an engineer view with everything you'd otherwise dig out of Task Manager, `ping`, `tracert`, `netsh` and Wireshark.

> **No cloud. No telemetry. Never listens.** Every packet it sends is listed in *About* — and you can switch each one off.

## What it does

- **Corner widget** — faded to 30%, never takes clicks, folds away behind your windows. CPU, memory, disk, network and a verdict at a glance.
- **Is it me or the network?** — five dots from your PC out to the internet; the first one that isn't green is where the trouble starts. Click a dot for details.
- **Connections** — every conversation your PC is having, which app owns it, its state; *Sniff* a row straight into Wireshark.
- **Your Wi-Fi / cable** — band, channel, channel width, signal in dBm, roaming support, who else shares your air (channel map), why Wi-Fi dropped (from Windows' own log), devices on your network, driver age, and on cable: **which switch port and VLAN you're on** (LLDP/CDP).
- **What's running** — every program on your PC explained in plain English, and whether it's safe to close.
- **Check a destination** — DNS, route, ping, TCP, TLS, HTTP, path MTU and an MTR-style trace, then **the path story**: who carries your traffic, where networks hand over, NAT, tunnels, firewalls and load balancing.
- **Health check**, **What changed** (with optional 7-day history), **Live capture** (protocols, top talkers, bytes per app, Teams call quality, rogue DHCP — needs Wireshark).
- Hover anything with a **?** cursor for an explanation: what it is, how it works, why it matters, what to do when it looks bad.

## Get it

**[adeeb111.github.io/AdeeboMeter](https://adeeb111.github.io/AdeeboMeter/)** — download, and the Theory of Everything as slides.

1. Download `AdeeboMeter-<version>-win64.zip` from [Releases](../../releases) and open it.
2. Double-click **AdeeboMeter.exe** and choose **Install**. (SmartScreen: *More info → Run anyway* — the exe isn't code-signed.)
3. Look at the bottom-right corner of your screen.

It installs just for you — `%LOCALAPPDATA%\Programs\AdeeboMeter\<version>`, a Start menu entry and an entry in
Settings > Apps — with no administrator rights. **Update**: run the new exe, choose *Update*; your settings come along.
**Remove**: Settings > Apps, or the tray menu. Rather not install? Choose *Just run it from here*.

Windows 10 or 11. The exe is built by GitHub Actions on a clean Windows machine with Microsoft's own .NET Framework
compiler from this exact source; its SHA256 is published with each release.

**Build it yourself** (nothing to install — the compiler ships inside Windows): double-click **`build_exe.cmd`**.
Releases are packaged by `release.ps1` and tagged `v<version>`; what changed is in [CHANGELOG.md](CHANGELOG.md).

## Use it

| To… | Do this |
|---|---|
| Open the engineer view | Click the tray icon, or press **Ctrl+Alt+A** |
| Go back to the corner | **Esc**, minimise, the X, or *Back to corner* |
| Quit completely | Right-click the tray icon → **Exit** (nothing keeps running) |
| Day / night | The sun/moon switch in the header |
| Tune how much it watches | **Settings** → Penny / Balanced / Engineer |

## Privacy and security

- Never listens on a port (it checks itself at every start), no cloud, no account, no telemetry, no auto-update.
- No admin rights; no shell; tools run by full System32 path; inputs validated; capture paths fenced in.
- TLS 1.2/1.3 only with Windows' certificate validation.
- Builds clean against Microsoft's .NET security and quality analyzers with every rule on.
- `AdeeboMeter.exe --selftest` runs 27 checks, including "never listens", leak tests and parser tests.

## How it works

Read **[The Theory of Everything](docs/THEORY-OF-EVERYTHING.md)** — every part of the code explained in plain words.

## Feedback and help

[Report a problem](https://github.com/adeeb111/AdeeboMeter/issues/new/choose) · [Ideas & questions](https://github.com/adeeb111/AdeeboMeter/discussions) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [Changelog](CHANGELOG.md)

---

Created by Adeeb Khan · [MIT License](LICENSE) — free to use, change and share; keep the copyright notice.
