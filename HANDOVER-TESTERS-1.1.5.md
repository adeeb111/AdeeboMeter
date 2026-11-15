# AdeeboMeter 1.1.5 - tester handover

**Branch:** `dev-1.1.5` · **Motto:** Know what's wrong. Show anyone. · **Status:** feature-complete for testing, not released.

Everything in 1.1.5 is listed in `CHANGELOG.md` (newest first). This page tells you how to start, what's new since the last
round, and what only a real Windows PC can prove. Automated tests already pass here (see "What's already tested").

---

## 1. Start in 5 minutes

1. Get the build from Adeeb (a zip - unzip it, run `AdeeboMeter.exe`; no admin needed). On first start it asks
   **Install** or **Just run it from here** - choose **Just run it from here** so the test build stays in its folder.
2. On first start it sits in the bottom-right corner. Click the tray icon to open the full window.
3. Every tool has an **Example** button: it fills made-up data so you see the picture and result without touching the network.
   Then press **Run** for the real thing.
4. Found something? Use **Report a problem** in the app (no account needed) or tell Adeeb. Include: what you clicked,
   what you expected, what happened, and a screenshot (**Save picture...** under any picture now makes one for you).

Test on: Windows 10 and 11 · a standard (non-admin) account · Wi-Fi and cable · 100%, 150% and 200% display scaling ·
light and dark theme · both layouts (top tabs / sidebar).

---

## 2. What's new in this round (15 Nov) - please cover these first

### A. Pictures for (almost) every tool - 178 of 191
Each tool draws its result: packets going back and forth (DNS, DHCP, TLS, 802.1X, SNMP, mail, SSH...), races (DNS servers,
cloud regions), hop-by-hop roads, staircases (joining a network, IPv6), tiles, gauges, timelines, ID cards for certificates.
- [ ] Open 15-20 tools at random, press **Example**: a picture appears, nothing overlaps, text fits.
- [ ] Press **Run** on 5 of them for real: the picture fills with *your* numbers (not the Example's).
- [ ] Packets animate only while the window is active; minimised or in the background it stops (watch CPU in Task Manager: ~0-1%).
- [ ] 150% and 200% scaling: no clipped labels, the sidebar shows "More... (n)" instead of cutting tabs off.

### B. Take any picture with you (the link bar under every picture)
`Save picture ... · Copy picture · Open on my phone · Send by Bluetooth · [Quick Share]`
- [ ] **Save picture ...** offers PNG, draw.io diagram (.drawio), JSON and CSV. Open the .drawio at app.diagrams.net - boxes
      and arrows should be movable. Open the CSV in Excel - columns line up, nothing runs as a formula.
- [ ] **Copy picture** -> paste into Teams/WhatsApp Web/Word: the picture appears with a small footer.
- [ ] **Open on my phone**: scan the QR with an iPhone and an Android phone. The phone page is live on adeebometer.com. Expected on the phone: the same picture
      redrawn, the result rows, **Copy · Share · Save image · Copy for AI · Save as PDF**.
- [ ] Do the QR test with the PC's internet **off** (unplug / airplane): the phone still opens the page on its own data.
- [ ] **Send by Bluetooth**: Windows' Bluetooth window opens and Explorer selects the PNG. Send to another PC and to an
      Android phone (tap Accept; it lands in Downloads). iPhones can't receive Bluetooth files - that's expected.
- [ ] **Quick Share** appears only if Google's Quick Share for Windows is installed. If you have it installed and the link is
      missing, tell us the folder it's installed in.

### C. QR code tool (Toolbox › QR code)
- [ ] The QR shows big right in the tool (no extra click). Phone page: verdict, path, graded numbers, two charts, what to do,
      **Send to my internet provider** (ready message), Share, PDF, Copy everything, Copy for AI.

### D. Report for my ISP (picture pages)
- [ ] Run it (2 min). Open the saved report in a browser -> Print -> Save as PDF. Pages: the answer + what's ruled out + what we
      ask the provider to check · the path hop by hop · every probe of the run · loss by hour · 7x24 grid · two routes · message.
- [ ] Do the page breaks fall sensibly in the PDF?

### E. For traders, server people, streamers
- [ ] **Market-open check**: GO / CAREFUL / HOLD with broker timing, clock check, background apps.
- [ ] **Service board**: type `yourrouter:80, 1.1.1.1:443, a-wrong-host:9`; one shows down. Put `5` in the second box ->
      it keeps watching every 5 min while the app runs; `off` stops it.
- [ ] **Uptime report** (after an hour of watching): % up, p50/p95, last time down.
- [ ] **Certificate calendar**: sites sorted by days left; one expiring <30 days is amber/red.
- [ ] **What changed on my server?**: run twice on the same host; the second run says "nothing changed". Change something
      (another host with the same name, or after a cert renewal) -> it lists the change.
- [ ] **Ready to stream?**: needs a speed test from today first (it says so). Bars: upload vs safe bitrate vs your bitrate.
- [ ] **OBS link** ⚠ never tested against a real OBS - please do: OBS 28+ -> Tools -> WebSocket Server Settings -> Enable,
      set a password. Run the tool with the password (the box hides it). While streaming you should see network drops vs
      encoder skips vs render lag. Wrong password -> "refused the password". OBS closed -> "can't reach OBS".
- [ ] **Trading session tape** and **After-stream report** ⚠ the full line chart only appears with a *real* recorded day:
      turn on broker watching on the Trading page during market hours / watch a stream with the Streaming page on, then open them.

### F. Companion - philosophers
- [ ] Settings › Companion › Persona: Marcus Aurelius (Stoic), Aristotle, Taoist, Confucius - lines change everywhere.
- [ ] Words of comfort: Marcus Aurelius / Aristotle / Taoism / Confucius show a real quote with its reference, only in calm moments.

### G. Motto and security
- [ ] Title bar, tray menu, installer, About show "Know what's wrong. Show anyone."
- [ ] Type a password in any password box (RADIUS, SNMP community, OBS): it's dots, never appears in a picture, export or phone link.
- [ ] CSV with a host like `=1+1` opens in Excel as text, not as 2.

---

## 3. Things only a real PC can prove (we can't test them here)
| Area | Why |
|---|---|
| PDF printing (case report, ISP report) | needs "Microsoft Print to PDF" |
| Bluetooth, Quick Share, Phone Link | real radios and phones |
| OBS link | real OBS |
| Sidebar / search box at 150-200% on a small screen | real DPI |
| Wi-Fi name and signal on a company PC with location off | Windows hides them - the app should say so, not break |
| Session tape / after-stream line chart | needs a real recorded session |

---

## 4. What's already tested (automatically, every build)
- **Self-test** (~4.5 min): every tool's picture before a run and with its Example, every export (.drawio, JSON, CSV),
  every phone link fits one QR code, QR codes decode (checked with a real decoder up to 615 bytes), hostile input
  (script tags, quotes, formulas, `../`, right-to-left marks, nulls, 5,000 characters) at every exit, buddy lines' rules.
- **Deep test** (~25 min): every page and tool opened repeatedly - memory, handles, GDI objects and threads must not grow.
- **Code check**: .NET analyzers with zero findings, plus banned patterns (unsafe deserializers, shell command lines).

## 5. Known limits (not bugs)
- iPhones can't take Bluetooth files from Windows - use Open on my phone.
- Ready to stream? needs today's speed test. Uptime report needs Service board watching to have run.
- Board watching runs only while AdeeboMeter is open (no service is installed, by design).
- Phone links show what they carry - anyone could hand-make a link; the page says so.
