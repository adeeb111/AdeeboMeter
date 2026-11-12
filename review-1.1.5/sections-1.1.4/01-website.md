# 01 – Public website and user-facing docs (AdeeboMeter 1.1.4)

Reviewer scope: docs/index.html, docs/privacy.html, docs/sitemap.xml, docs/favicon.svg, docs/media/*, docs/THEORY-OF-EVERYTHING.md (+ PDF), README.md, CHANGELOG.md (1.1.4), SECURITY.md, CONTRIBUTING.md, .github/ISSUE_TEMPLATE/*, .github/FUNDING.yml, docs/LIVE-TESTS.md.

Method: the live site (adeeb111.github.io) and every non-GitHub host (paypal.me, youtube.com, signpath.org, ripe.net, peeringdb.com) are blocked by this sandbox's proxy (curl returns 000). I copied docs/ to a scratch folder, served it with `python3 -m http.server 8765`, and drove it with Playwright Chromium. Runs covered desktop 1366x768, iPhone 13, iPhone SE/320, 360, 375, 384, tablet 820x1180, light and dark, at deviceScaleFactor 1. I also ran axe-core 4.10.2. github.com and api.github.com were reachable, so the release/asset checks are real. Screenshots are in `/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review/web-shots/`.

Clean results:
- No console errors, page errors or 404s at any size or in either colour scheme, on index or privacy.
- All in-page `#` anchors resolve.
- Every `<img>` has alt text.
- Heading order outside the slides is fine: one H1, then H2 and H3.
- Both mp4s are H.264 with `moov` before `mdat` (fast start). The trailer is silent, autoplays muted and has a poster. The tour has sound, `preload="none"`, no autoplay and a poster.
- The download asset name the page JS looks for (`-win64.zip`) matches what release.ps1 builds and what the v1.1.4 release ships (`AdeeboMeter-1.1.4-win64.zip`, SHA256 matches SHA256SUMS). The zip holds the exe, the PDF, LICENSE, READ ME and SHA256SUMS, as the site says.
- "42 security checks" matches the code: `Security.All` has 27 pc + 11 net + 4 sites, and the self-test enforces 42.
- Suggestion-box input is safely `encodeURIComponent`-ed. `<img onerror>`, `<script>`, `%00`, RTL override and emoji only end up as URL-encoded text.
- SECURITY.md anchor `#7-security-in-short` resolves.

---

## High

### H1. The privacy policy page (privacy.html, used for the Store listing) leaves out most of the traffic and contradicts the main site, README and the in-app About
- **Where:** docs/privacy.html:43-49 ("What it sends, and when"); compare docs/index.html:817-825, README.md:95-102, src/Ui/FullView.Settings.cs:265-277, src/Checks/Protocols.cs:106-109, src/Checks/SecChecks.cs:364-381.
- **Repro:** Read privacy.html "What it sends", then the "What leaves your PC" slide on index.html (slide 19) or the About tab in the app.
- **What happens:** privacy.html lists only three kinds of traffic: a ping to 1.1.1.1 and 8.8.8.8 every ~30 s, tools you start, and "online lookups (for example a DNS-over-HTTPS server)". It leaves out:
  - the router ping every 2–10 s;
  - the DNS lookup that goes with each internet check;
  - the once-per-network 6-hop trace toward 1.1.1.1 plus a reverse-DNS lookup;
  - the msftconnecttest.com sign-in page probe;
  - the opt-in pinned-destination watch;
  - the actual online-lookup recipients, RIPEstat (stat.ripe.net) and PeeringDB. These are the main third parties, and the page never names them.

  It also says DoH checks "run only if you turn on online lookups". That is false for the Toolbox "Encrypted DNS (DoH / DoT)" tool (`Protocols.EncryptedDns`), which calls cloudflare-dns.com and dns.google with no online-lookups gate. Finally, "Set the internet check to Off and it stops completely" sits in a section that never mentions the router ping, which keeps running.
- **Expected:** The legal policy page lists the same traffic as About and the slide, and names RIPEstat, PeeringDB, Cloudflare/Google DoH and msftconnecttest.
- **Fix:** Rebuild the "What it sends" list from the `leaves` table in FullView.Settings.cs, the same source the slide uses. Replace the DoH example with "RIPEstat and PeeringDB (public IP addresses only)". Say that the Encrypted DNS tool and the clock check contact Cloudflare, Google and Microsoft when you press Run.

## Medium

### M1. The "online lookups only send public IPs to RIPEstat and PeeringDB" claim is false: with lookups on, the Security checkup also contacts Cloudflare DoH and www.microsoft.com
- **Where:** README.md:78 and :101; docs/index.html:824 and :965; About row (FullView.Settings.cs:274). Code: src/Checks/SecChecks.cs:365 (`QueryDoh(new Uri("https://cloudflare-dns.com/dns-query"), "www.microsoft.com", ...)`) and :381 (TLS handshake to www.microsoft.com:443), both gated on `Online.On`.
- **Repro (code read):** Turn on online lookups, then run the Security checkup. "DNS vs an encrypted resolver" sends a DoH query for www.microsoft.com to cloudflare-dns.com. "Encrypted traffic inspected?" opens TLS to www.microsoft.com.
- **What happens:** These are names, not public IPs, and the recipients are neither RIPEstat nor PeeringDB. The site and README say online lookups "send public IP addresses only ... to RIPEstat and PeeringDB", and About's "Online lookups (BGP)" row also names only stat.ripe.net and peeringdb.com. "Every packet it sends is listed in About" therefore doesn't hold for the Security checkup.
- **Fix:** Add a row for "Security checkup (with online lookups): one DoH question to cloudflare-dns.com, one TLS handshake to www.microsoft.com" to About, the slide, README and privacy.html. Alternatively, change the wording to "public IP addresses to RIPEstat/PeeringDB, plus two fixed checks to Cloudflare and Microsoft".

### M2. Phones narrower than 388 px: the header overflows, the Download pill is clipped and the page pans sideways
- **Where:** docs/index.html:50-57 and :295-298 (`.top .wrap` flex row: brand + heart + theme toggle + Download pill, with no wrap or shrink).
- **Repro:** Playwright with `devices['iPhone SE']` (320), or a 360/375/384 px viewport with `isMobile:true`. Load `/` and read `innerWidth` and `scrollWidth`.
- **What happens:** The layout viewport grows to 388 px at every width tested (360, 375, 384 → `innerWidth: 388`, `visualViewport.width` = device width). The `.pill` "Download" button spans x=288–388, so at 375 px its right edge is cut off and at 320 px only "Do" shows. That covers 360 px Androids, iPhone SE 2/3 and iPhone mini (375). Screenshots: web-shots/nav-375.png, nav-iPhone_SE.png. privacy.html also overflows at 320 px (339 px wide) because the long link text `github.com/adeeb111/AdeeboMeter/issues` can't wrap.
- **Fix:** Below about 400 px, shrink the brand text or hide the wordmark, or drop the pill padding to 6px 12px, and give `.top .wrap` a `gap` with `min-width:0`. On privacy.html add `a{overflow-wrap:anywhere}`.

### M3. With api.github.com unreachable, the hero says "Version 1.1.3", the footer says "Version 1.0", and the in-app "Check for updates" shows nothing
- **Where:** docs/index.html:363 (`Version 1.1.3`), :1003 (`Version 1.0`), :21 (JSON-LD `"softwareVersion":"1.1.3"`), :1088-1108 (update banner only renders inside the API `.then`). App side: src/Ui/FullView.Settings.cs:294 opens `?v=<ver>#update`.
- **Repro:** Load `/?v=1.1.3#update` with `api.github.com` answering 403 (rate limit), or blocked by an ad blocker or corporate proxy. I simulated this in pw9.js.
- **What happens:** The banner stays hidden and the two version lines read "Version 1.1.3" and "Version 1.0". The download button falls back to `/releases/latest`. Unauthenticated API calls are capped at 60 per hour per IP, so users behind a shared corporate NAT will hit this, and an app user who clicked "Check for updates" gets no answer at all. Search engines and link unfurlers always see JSON-LD 1.1.3 and never run the JS.
- **Fix:** Update the static fallbacks to 1.1.4 (both `.ver` spans, JSON-LD, slide kicker at :451, self-test mock at :873) and add them to the release checklist in CONTRIBUTING.md. When the fetch fails and `?v=` is present, show "Couldn't check right now — see GitHub Releases" with a link.

### M4. PageDown and PageUp are hijacked across the whole page while the slide deck is in view
- **Where:** docs/index.html:1038-1043.
- **Repro:** Desktop 1366x768. Scroll until the deck fills the middle of the screen, then press PageDown repeatedly.
- **What happens:** `scrollY` stays at 3082 while the counter goes 2/26 → 3/26 → 4/26 (pw7.js). A keyboard user paging down the site is stuck for up to 25 presses. ArrowLeft and ArrowRight are also taken globally, not only when the deck has focus.
- **Fix:** Handle keys only when `stage.contains(document.activeElement)` or in fullscreen, and leave PageUp/PageDown to the browser.

### M5. On phones the slide deck leaves an ~880 px blank gap under most slides
- **Where:** docs/index.html:185-188 and :302-303 (`.slide{aspect-ratio:auto}` on ≤860 px; flex row stage, `align-items:flex-start`).
- **Repro:** iPhone 13 emulation, scroll to "Every part, in plain words."
- **What happens:** The stage is as tall as the tallest slide (1273 px). Slide 1 is 398 px, so about 880 px of empty space sits between the slide and the "1 / 26" bar. Slide heights range from 291 to 1273 px. See web-shots/phone-dark-full.png and the crop scratchpad/pd_04000.png.
- **Fix:** Give the stage `align-items:flex-start` and set its height to the current frame on scroll, or on mobile drop the carousel and stack the slides vertically.

### M6. Dark mode: primary buttons and the Donate link fail WCAG AA contrast
- **Where:** docs/index.html:33/38 (dark `--blue:#4C8DF6`), :55 and :83 (white text on `--blue`), :57 (`#D1245B` on dark page).
- **Repro:** axe-core on `/` with `colorScheme:'dark'`.
- **What happens:**
  - "Download" pill and "Download for Windows": white on #4C8DF6 is 3.25:1 at 14–17 px, weight 600. That isn't large text, so the AA minimum is 4.5:1.
  - "Donate" #D1245B on #0A0E13 is 3.79:1.
  - In light mode, the ghost buttons ("See how it works", "Download PDF") are 4.04:1 (#1F6FEB on #E8F0FE), `.meta` is 4.49:1, and the slide tags are 4.04–4.47:1.
  - The footer "MIT License" link differs from the surrounding text by 1.6:1 with no underline.
- **Fix:** In dark mode use `--blue:#2F6FDB`-ish for button backgrounds (or dark text on the light blue). Lighten dark-mode Donate to about #F0608A. Darken the light `--blue` on `--blue-s` backgrounds to about #1A5FCC. Underline footer links.

### M7. "103 tools" is wrong in README, CHANGELOG and the Theory of Everything: the code registers 105
- **Where:** README.md:32 ("100+ tools (103)"), CHANGELOG.md:7 ("103 in all"), docs/THEORY-OF-EVERYTHING.md:235. Code: src/Ui/FullView.Toolbox.cs:29-… (`Tools2()` list) and :144-158 (`TbLayout`).
- **Repro:** Count `T("id", …)` entries in `Tools2()`: 105 unique ids, and the same 105 ids appear in `TbLayout`. That is 95 in the four Toolbox sections plus 10 "THE PC" tools under Investigate › Health. The deep test also prints `Tools2().Count`.
- **Fix:** Say "105 (95 in the Toolbox, 10 in Health)", or keep "100+" only. A self-test assert on the number in the docs would stop it drifting again.

## Low

### L1. Stale version strings across the user-facing docs
- docs/index.html:21 JSON-LD `softwareVersion:"1.1.3"`; :363 "Version 1.1.3"; :451 "version 1.1.3"; :873 "AdeeboMeter 1.1.3 self-test"; :1003 "Version 1.0".
- docs/THEORY-OF-EVERYTHING.md:3 "Version 1.1.3". docs/AdeeboMeter-Theory-of-Everything.pdf, and the PDF inside the released v1.1.4 zip, still show "AdeeboMeter 1.1.3 self-test" (pdftotext).
- .github/ISSUE_TEMPLATE/problem.yml version placeholder "1.0"; index feedback placeholder "AdeeboMeter 1.1".
- By contrast, Program.cs, CHANGELOG, the release tag and READ ME.txt all say 1.1.4.
- **Fix:** Bump them all and add "grep for the old version" to the release steps in CONTRIBUTING.md.

### L2. Spoofable text in the update banner via `?v=`
- **Where:** docs/index.html:1099-1104.
- **Repro:** `/?v=Please+download+the+fix+from+evil.example#update`.
- **What happens:** On the official domain, in the site's own blue "new version" style, the banner reads "AdeeboMeter 1.1.4 is out - you have Please download the fix from evil.example. Download it below…" (web-shots/update-banner-spoof.png). It uses textContent, so this is not XSS. `<img onerror>` showed as text and no dialog fired. Separately, `?v=2.0` and `?v=1.1.04` report "up to date – 2.0 is the latest version", which is factually wrong.
- **Fix:** Show the banner only when `mine` matches `/^\d+(\.\d+){1,3}$/`. For a version newer than the latest, say "you're ahead of the latest release".

### L3. privacy.html is an orphan
- **Where:** docs/index.html (no `privacy.html` link anywhere; the nav "Privacy" goes to the `#privacy` marketing blurb) and docs/sitemap.xml.
- **Repro:** `grep -n privacy.html docs/index.html` finds nothing. sitemap.xml lists only `/` and the PDF.
- **Fix:** Link it from the footer and from the `#policy` "Privacy policy" card, and add it to the sitemap.

### L4. Sitemap `lastmod` is stale and there is no robots.txt
- **Where:** docs/sitemap.xml.
- **What happens:** `lastmod` is 2026-11-09, but index.html changed on 2026-11-11 (videos and YouTube links). There is no robots.txt to advertise the sitemap.
- **Fix:** Update `lastmod` and add `docs/robots.txt` with a `Sitemap:` line.

### L5. Slide footer numbers disagree with the counter
- **Where:** docs/index.html slide `.foot` spans.
- **What happens:** There are 26 frames and the counter shows "1 / 26", but the footers read 01, 01, 02 … 24, 24 (the hero and slide 2 are both "01"; "Get it" and the end slide are both "24"). The static fallback text is "1 / 24".
- **Fix:** Renumber the footers 01–26, or generate them in JS.

### L6. "One readable file / One C# file" is no longer true
- **Where:** docs/index.html:519 (slide "Five promises"); CONTRIBUTING.md:20 ("One readable file, C# 5").
- **What happens:** `src/` has 64 .cs files (19.5k lines). CONTRIBUTING.md:11 and THEORY-OF-EVERYTHING.md:28 already say "one file per part".
- **Fix:** Use "Readable source — plain C#, built by the compiler inside Windows".

### L7. Self-test claims are out of step with the code
- **Where:** README.md:83, docs/index.html:869, docs/THEORY-OF-EVERYTHING.md:281 and :285.
- **What happens:** These say "30 checks", but src/SelfTest.cs registers 35 `T("…")` checks. The site and the Theory of Everything say "engineer view opens in under 1 second", while the test is "under 2 s" and fails only above 2000 ms (SelfTest.cs:228-237).
- **Fix:** Say "35 checks" (or "30+") and "in under 2 s (aim: 1 s)".

### L8. The page says it loads nothing from elsewhere, but it calls api.github.com on every visit
- **Where:** docs/index.html:24 (CSS comment "This page loads nothing from anywhere else") and :1088-1089 (fetch).
- **What happens:** Every visitor's IP and Referer go to GitHub's API (confirmed in Playwright: `200 https://api.github.com/repos/adeeb111/AdeeboMeter/releases/latest`). It's only a source comment, but the site's privacy positioning relies on it.
- **Fix:** Fix the comment and mention it in privacy.html ("the website asks GitHub for the latest version number").

### L9. The tour video with voice-over has no captions
- **Where:** docs/index.html:419. Neither `<video>` has a `<track>`.
- **What happens:** WCAG 1.2.2 (captions, prerecorded) fails for the 1 min 23 s narrated tour. The trailer autoplays and loops, ignoring `prefers-reduced-motion` (the CSS reduced-motion rule covers only the hero animations). Autoplay also overrides `preload="metadata"`, so mobile users download the 2.6 MB trailer straight away.
- **Fix:** Add `<track kind="captions" src="media/tour.en.vtt" srclang="en" default>`. In JS, pause and remove autoplay when `matchMedia('(prefers-reduced-motion: reduce)')` matches, or when `navigator.connection.saveData` is on.

### L10. Keyboard and screen-reader polish
- **What happens:**
  - The `.stage` carousel (tabindex=0) has `outline:none` (index.html:185). When it has focus there is no visible focus indicator (`:focus-visible` true, outline none).
  - The theme switch has no `aria-pressed` or state, only "Switch day / night".
  - Below 860 px every nav link except Donate and Download is hidden and there is no menu, so mobile users can't jump to Install, Feedback or GitHub (index.html:296).
- **Fix:** `.stage:focus-visible{outline:2px solid var(--blue);outline-offset:4px}`. Add `aria-pressed` to the theme button. Add a compact menu or keep 2–3 key links on mobile.

### L11. privacy.html ignores the visitor's chosen theme
- **Where:** docs/privacy.html:12-14.
- **What happens:** The page has the `data-theme` CSS but no script reading `localStorage['am-theme']`. A visitor who picked Light on index with an OS in dark mode gets a dark privacy page.
- **Fix:** Copy the one-line theme script from index.html:327.

### L12. README badge says "Offline" for a tool that pings the internet by default
- **Where:** README.md:9 (alt "Offline" on the telemetry badge), :13 ("Free, offline network troubleshooter").
- **What happens:** By default the app pings 1.1.1.1 and resolves a name every 30 s (Program.cs:5-7). "Local-first" (the site's wording) is accurate; "offline" isn't.
- **Fix:** Use "local-first" to match the site.

### L13. Suspected: the suggestion box may drop the user's text for Ideas, and the bug label may not apply
- **Where:** docs/index.html:1077-1083; .github/DISCUSSION_TEMPLATE/ideas.yml.
- **What happens (Suspected, couldn't test because github.com/…/discussions returns 403 through the proxy and GraphQL is blocked):**
  - The Ideas category has a discussion form template (field id `idea`). With a form template, GitHub prefills form fields by their id, not from `body=`, so the text typed on the website may be lost. The URL the site builds is `…/discussions/new?category=ideas&title=…&body=…`.
  - `labels=bug` on `/issues/new` is only applied for users with triage rights.
  - The `q-a` category slug is assumed and not verified.
  - A 4000-emoji body builds a 24,242-character URL, which may be rejected (414) by GitHub.
- **Fix:** Use `&idea=<text>` for the Ideas form (the field id). Use `template=problem.yml&what=<text>&version=<env>` for problems. Cap the body length for the URL at about 6,000 encoded characters.

### L14. Suspected: privacy.html "Microsoft Store" section describes a distribution that may not exist yet
- **Where:** docs/privacy.html:54-55.
- **What happens:** Nothing in README, the site or the releases mentions a Store listing. Not verified, since I couldn't query the Store.
- **Fix:** Keep the section only once the listing is live, or add "if available".

### N/A: Donate box "Other amount" injection test
- The donate box (docs/index.html:1006-1017) has no input field. "Other amount" is a plain link to `https://paypal.me/adeeb1111`, and the fixed amounts are static `paypal.me/adeeb1111/<n>CAD` links with `rel=noopener`. There is no URL-building surface, so the garbage, negative, 0, huge, emoji and XSS cases don't apply.
- PayPal, YouTube, SignPath, RIPE and PeeringDB link status could not be checked (proxy blocks those hosts).
- GitHub links that were checked: releases/latest → 302 to v1.1.4, issues 200, LICENSE 200, THEORY-OF-EVERYTHING.md 200, API 200. discussions returned 403 via the proxy, but the API says `has_discussions: true`.
