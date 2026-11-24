# Live tests - what only real Windows and real gear can prove

The self-test and the deep test (`--selftest`, `--deeptest`) cover parsing, hostile input, leaks and every page. These are the checks they can't do: they need a real network, real devices or a person looking. Tick each one on the release candidate; a "can't tell" is fine where the note says so.

## Security checkup (Security › Run security checkup)
- [ ] Home PC: no red that isn't true. Compare firewall, BitLocker, Secure Boot, UAC and Windows Update with Windows Security and Settings.
- [ ] Company PC (standard account, domain): runs without admin; firewall, LSA / Credential Guard and memory integrity agree with Windows Security › Device security; proxy and Remote Desktop say "set by your company" where they are.
- [ ] Turn the firewall off for one profile (home PC) -> red; back on -> green.
- [ ] A site you type (e.g. your own) shows certificate days, TLS, headers and SPF / DMARC; a mail domain without DMARC is flagged.
- [ ] "Open the setting" opens the right Windows screen for five different checks.

## What's wrong? (Investigate)
- [ ] Working network -> "Nothing is wrong right now" (or names a weak layer honestly).
- [ ] Wi-Fi off -> stops at "2. The link".
- [ ] Router unplugged (or another router's network with no internet) -> stops at "3. Your router" or "4. The internet".
- [ ] Hotel / airport Wi-Fi before signing in -> "6. The way in" names the sign-in page.
- [ ] Company network that blocks ping -> the internet layer passes over TCP, never red.

## Traffic Lab
- [ ] Company PC: every sender refuses to send until the tick is set.
- [ ] ICMP builder: `size 1472 df` to your router answers; `size 1500 df` reports "too big" on a 1500-MTU path; `ttl 2` names the second hop.
- [ ] UDP sender to a closed port says "port closed"; TCP sender to a web server with `GET / HTTP/1.0\r\n\r\n` shows the reply.
- [ ] ARP who-has your router -> its MAC matches `arp -a`.
- [ ] DHCP options from the server: router, DNS and lease match `ipconfig /all`.
- [ ] Test trap and NetFlow record arrive at a real receiver / collector.
- [ ] Stop ends a 1,000-packet run at once.

## SNMP on real devices
- [ ] Interfaces twice, five minutes apart, on a switch with a port taking errors -> "Since HH:mm" shows the growth.
- [ ] Find a MAC: a PC's MAC (and its IP via the router) lands on the right switch port; an uplink port is called an uplink.
- [ ] VLANs and PVIDs match the switch's own `show vlan`.
- [ ] PoE: budget and a powered phone / AP match the switch; LAG: members and partner match `show lacp`.
- [ ] Printer: toner and an open door / jam; UPS: on mains, then unplugged -> "ON BATTERY" and minutes left.

## Devices and certificates
- [ ] Name of a device names a Windows PC and a phone.
- [ ] Shared folders on a NAS match File Explorer.
- [ ] Remote access: a copied ssh line works in PowerShell; the top line opens PowerShell.
- [ ] Check a PFX exported by Windows (AES) and one from OpenSSL with `-legacy`: both open, key "present and usable".
- [ ] Hash a downloaded release file against SHA256SUMS -> "Matches".

## Widget
- [ ] Multi-monitor company PC: the widget is still there after sleep, unlock and docking; tray › Widget on screen moves it.
- [ ] Internet check off in Settings: ISP and Internet grey, never red, also with the main view open.
