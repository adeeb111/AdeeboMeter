# Changelog

Facts only: what each version reads, shows and checks. Created by Adeeb Khan.

## 1.0 (2026-11-07)

The first public version: a quiet Windows companion that tells you, in one glance, whether your PC, your Wi-Fi, your provider, DNS or the internet is the problem — and what to do about it.

### Highlights
- **Corner widget** — faded, click-through, folds into a small chip when a window covers the corner. One sentence
  verdict, five dots from your PC out to the internet.
- **Engineer view** (tray icon or Ctrl+Alt+A) — Connections with *Sniff* into Wireshark, Your Wi-Fi / cable, What's
  running, Check a destination, Health check, What changed, Live capture.
- **Your Wi-Fi** — band, channel and width, signal in dBm, roaming support, neighbouring networks on your channel, why
  Wi-Fi dropped. **Your cable** — speed, and the switch, port and VLAN you're plugged into (LLDP / CDP).
- **Check a destination** — DNS, route, ping, TCP, TLS, HTTP, path MTU and a trace, then the path story: who carries
  your traffic, NAT, tunnels, firewalls and load balancing.
- **Live capture** (with Wireshark) — what your traffic is made of, bytes per app, Teams call quality, rogue DHCP.
- **Plain English everywhere** — hover anything with a **?** cursor; 114 terms and 72 programs explained.
- **Light by design** — Penny / Balanced / Engineer presets; about half a percent of one CPU core in the background.

### New in 1.0
- **One file installs itself**: double-click AdeeboMeter.exe and choose *Install*, *Update* or *Just run it from here*.
  Per user (`%LOCALAPPDATA%\Programs\AdeeboMeter\<version>`), no administrator rights, a Start menu entry and an entry
  in Settings > Apps; your settings and "Start with Windows" come along.
- **Remove** from Settings > Apps or the tray menu (*Uninstall AdeeboMeter...*) — folder, settings and history.
- **Updates** ask the running copy to step aside, then replace it; older versions are removed.
- **Icon** on the exe, the Start menu and Settings > Apps.
- **Website** with a download button, the Theory of Everything as slides, and a suggestion box:
  https://adeeb111.github.io/AdeeboMeter/

### Privacy and security
- No cloud, no account, no telemetry, no auto-update; never opens a listening port (checked at every start).
- What it sends is listed in *About* with your current settings, and each item can be switched off.
- No administrator rights, no shell; Windows tools by full System32 path; TLS 1.2 / 1.3 with Windows' certificate
  checks; builds clean against Microsoft's .NET security and quality analyzers with every rule on.

### Known limitations
- Not code-signed yet: SmartScreen asks once (*More info > Run anyway*).
- No AS numbers / BGP data (would need an internet service); traces use ICMP (TCP/UDP traces need admin).
- Wi-Fi details on Windows 11 need *Location* allowed for desktop apps — AdeeboMeter says which switch to flip.
- Live capture and *Find my switch port* need Wireshark (tshark + Npcap).

### Under the hood
- Releases are tagged `v<version>` and built by `release.ps1` on GitHub's Windows runner; Setup.cmd / Uninstall.cmd
  are gone — the app does both.
- Self-test: 27 checks (new: install paths, the settings line, the icon).

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
