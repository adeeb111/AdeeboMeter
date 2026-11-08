# Changelog

Facts only: what each version reads, shows and checks. Created by Adeeb Khan.

## 1.0 (2026-11-07)

The first public version.

- **One file installs itself**: double-click AdeeboMeter.exe and choose *Install*, *Update* or *Just run it from here*.
  It installs just for you (`%LOCALAPPDATA%\Programs\AdeeboMeter\<version>`, no administrator rights), adds a Start
  menu entry and an entry in Settings > Apps, and carries your settings and "Start with Windows" over.
- **Remove** from Settings > Apps or the tray menu (*Uninstall AdeeboMeter...*): it stops, leaves Start and Settings >
  Apps, and removes its folder, settings and history.
- **Updates** ask the running copy to step aside, then replace it; older versions are removed.
- **Icon**: the exe, the Start menu and Settings > Apps show the AdeeboMeter logo.
- **Website** with the Theory of Everything as slides to swipe through or download:
  https://adeeb111.github.io/AdeeboMeter/
- **Releases** are tagged `v<version>`; the zip carries the app, READ ME and the Theory of Everything PDF. Setup.cmd
  and Uninstall.cmd are gone - the app does both.
- **Self-test**: 27 checks (new: install paths, the settings line, the icon).

## beta-0.6.3 (2026-11-07)

Release pattern, steadier self-test.

- **Download**: a versioned zip with Setup.cmd / Uninstall.cmd (replaced in 1.0 by the exe installing itself);
  releases built by `release.ps1` on GitHub's Windows runner, notes from this changelog.
- **Build**: `build_exe.cmd` replaces build.ps1 (double-click; no PowerShell execution policy in the way).
- **Self-test**: the handle-leak check opens and closes the engineer view 30 times and allows 10 handles of drift (the
  old 12-handle failure was measurement noise); closing the view drops its icon and hover card explicitly.

## 0.6.2 and earlier (before the release pattern)

Fixes from the first runs on a real PC:

- **Your PC**: TCP rows Windows reports with state 0, or closed with no remote end, show as BOUND.
- **DNS cache**: when Windows needs elevation to show it, the view says so instead of showing it empty.
- **What's running**: programs under your user profile (Documents and the like) count as "yours", not "elsewhere".
- **Your Wi-Fi / cable**: with no adapter matched, the first active adapter is used, else one plain sentence.
- **ISP**: learned once from a 6-hop trace to 1.1.1.1 and the reverse name of the first public hop ("provider: …").

What 0.6 brought - renamed to AdeeboMeter (settings and "start with Windows" of the old name move over by themselves).

- **Your Wi-Fi**: band, channel, channel width (HT / VHT / HE), signal in dBm, 802.11k/v/r roaming support, the channel
  map of who else is on the air, why Wi-Fi dropped (Windows' WLAN event log), devices on your network (ARP), driver age.
- **Your cable**: speed and duplex, and from LLDP / CDP (needs Wireshark, on demand) the switch, port and VLAN.
- **The path story**: who carries your traffic (reverse names), where networks hand over, private / CGNAT / public
  hops, PPPoE (MTU 1492), load balancing, firewalls inferred from refusals, the return path from reply TTLs.
- **Live capture**: Teams / RTP call quality (jitter RFC 3550, MOS), rogue DHCP servers.
- **Header**: day / night switch, About, Settings, Back to corner.
- **Settings**: Penny / Balanced / Engineer (18 / 36 / 74 checks a minute); optional 7-day history.
- **Explanations**: every process and every term on hover (what it is, how it works, why it matters, what to do).
- **Captive portal**: Windows' own NCSI probe; DNS second opinion via 1.1.1.1 / 8.8.8.8.
