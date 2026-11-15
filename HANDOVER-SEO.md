# AdeeboMeter website - SEO handover

For the agent taking over SEO for **https://adeebometer.com**. Read the rules first - they are the owner's, not suggestions.

## Rules (hard)
1. **No company/employer references anywhere** (no Nokia, no product names of employers). Generic wording only.
2. **No tracking**: no Google Analytics, no tag managers, no cookies, no third-party scripts that phone home. The product's
   promise is "no cloud, no telemetry, never listens" - the site must match it. Search Console (no on-page script) is fine.
3. **Don't mention GitHub on the site** except the About/source link (his friends couldn't log in there). Downloads and bug
   reports go through the site/app, not GitHub pages.
4. **Never publish a release** or merge to `main` on your own - prepare a branch/PR and the owner merges.
5. **Investing pages/text are education only** - no advice, no tickers, made-up numbers.
6. Motto: **"Know what's wrong. Show anyone."** - the Home question "Is it me, my PC or the network?" stays as the hook.

## How the site is built and published
- Static pages in the repo's `docs/` folder, served by **GitHub Pages from `main` /docs**, custom domain via `docs/CNAME`
  (`adeebometer.com`, registered at Spaceship, DNS pointing to GitHub Pages).
- Work happens on a branch (e.g. `site-seo`), then a PR to `main`. `dev-1.1.5` is the app branch - its `docs/` changes
  (new phone page `r.html`, new motto on `index.html`) reach the live site only when it's merged to `main`.
- Plain HTML + inline CSS, no build step, no framework. Keep pages fast (they are).

## Pages (as of 15 Nov 2026)
| Page | Title | In sitemap | Notes |
|---|---|---|---|
| `/` index.html | AdeeboMeter — free Windows network troubleshooter (no admin, no cloud) | ✓ | canonical, OG tags, JSON-LD SoftwareApplication with `installUrl` + `sameAs` (Store) |
| `/privacy.html` | AdeeboMeter Privacy Policy | ✓ | |
| `/gaming.html` | AdeeboMeter for gaming - whose lag is it? | ✓ | |
| `/streaming.html` | AdeeboMeter for streaming - why did it buffer? | ✓ | |
| `/trading.html` | AdeeboMeter for trading - is it your connection or the broker? | ✓ | |
| `/hall-of-fame.html` | Hall of Fame — AdeeboMeter | ✓ | |
| `/AdeeboMeter-Theory-of-Everything.pdf` | the plain-language explainer | ✓ | |
| `/r.html` | AdeeboMeter Result (phone page for QR links) | ✗ | `noindex` on purpose - keep it out of the sitemap and search |

`robots.txt` allows all and points to `/sitemap.xml`. Microsoft Store: **9PFGXJPL9CLJ**
(`https://apps.microsoft.com/detail/9PFGXJPL9CLJ`) - the badge and JSON-LD already link it.

## Search Console status
- Property verified for adeebometer.com; **sitemap submitted - Success**.
- Indexing requested for `/` and `/privacy.html` on 14 Nov. Check Pages report for what's indexed; request the rest.

## What the product is (for copy and keywords)
Free Windows app (Store + direct download), local-first, no admin rights, no cloud account. It answers "is it my PC, my Wi-Fi,
my provider, DNS or the internet?" in one glance, then **shows it as a picture you can send** - to your ISP (visual ISP report),
a friend, your phone (QR), or an AI. 190+ tools with pictures: DNS, DHCP, TLS/certificates, ping/trace/MTR, Wi-Fi, SNMP, mail,
SSH, VPN leaks, IPv6, bufferbloat, speed. Modes: Gamer Zone (whose lag is it), Streaming (why did it buffer), Trading (broker
latency, market-open check), servers (service board, uptime, certificate calendar), streamers (go-live check, OBS frame drops).
Companion with moods and personas (incl. Stoic, Aristotle, Taoist, Confucius).

**Keyword angles worth pages or sections** (search intent, low competition, matches real features):
- "is it my wifi or my internet" / "is it me or my ISP" -> the core hook
- "how to prove to my ISP the problem is on their side" / "ISP report packet loss" -> the visual ISP report
- "why does my stream buffer" / "OBS dropped frames network" -> streaming + OBS link
- "whose lag is it" / "game lag wifi or server" -> gaming page
- "broker latency check" / "is my connection fast enough to trade" -> trading page
- "free DNS test windows" / "dig for windows" / "check certificate expiry" -> tool landing sections
- "network troubleshooter no admin rights" -> work-PC users

## Suggested next steps (owner decides)
1. Merge-ready: make sure `r.html` stays `noindex` and out of the sitemap after the next merge.
2. One focused landing section (or page) for the **ISP report** ("prove it's not your Wi-Fi") with a screenshot - strongest intent.
3. Screenshots with descriptive `alt` text on each page (the new tool pictures are good material - ask the owner for PNGs;
   every picture has Save picture in the app).
4. `og:image` per page (1200x630) - currently check which pages have one.
5. FAQ blocks with `FAQPage` JSON-LD on index/gaming/streaming/trading (real questions, short answers).
6. Bing Webmaster Tools too (Windows users; the Store is Microsoft) - import from Search Console.
7. Keep Core Web Vitals green: no new scripts, images sized and lazy-loaded, YouTube via click-to-load (already done).
8. Add `lastmod` to sitemap entries when pages change.

## Don't
- Don't add analytics, chat widgets, cookie banners or ad scripts.
- Don't index `r.html`, `HANDOVER-*`, `PLAN-*` or `.md` files under `docs/`.
- Don't change the app's wording of privacy promises without the owner - they must match the app and the privacy page.
