# AdeeboMeter website - SEO handover

For whoever takes over SEO for **https://adeebometer.com**. Updated 17 Nov 2026 (first version 15 Nov).
Read the rules first - they are the owner's, not suggestions.

## Rules (hard)
1. **No company/employer references anywhere** (no employer or vendor names, and none of their product names - see the private brief). Generic wording only.
2. **No tracking**: no Google Analytics, tag managers, cookies, pixels or third-party scripts that phone home. The product
   promises "no cloud, no telemetry, never listens" - the site must match. Search Console / Bing Webmaster (no on-page
   script) are fine.
3. **Don't mention GitHub on the site** except the About/source link (his friends couldn't log in there).
4. **Never publish** or merge to `main` yourself - prepare a branch and the owner merges.
5. **Investing pages are education only** - no advice, no tickers, made-up numbers.
6. **Copy rules:** short, plain, friendly; no hype; every claim must be true of the app today.
7. Motto: **"Know what's wrong. Show anyone."** Headline (decided 17 Nov): **"Find out why your internet or PC is slow.
   And much more."**

## What changed since 15 Nov
- **1.1.5 released 17 Nov** (GitHub + Microsoft Store), **code-signed** as "Open Source Developer MdAhasanul Adeeb Khan"
  (Certum). The site now says so - use "free, code-signed" as a trust point.
- **New home page top** (live 17 Nov): navy look by default (light mode via the switch), the headline + motto above, an
  example result (This PC > Router > DNS > Internet with "Send to my ISP"), phone + PC linked, signed-by badge, trust line
  (Free · No admin rights · No cloud, no telemetry · Never listens · Signed by its author).
- **New sections live on the site**: the phone app (`/app.html`), Live (`/live.html`), Games (`/games.html`), Files
  (`/files.html`) and **Learn with 91 short lessons** (`/learn/...`) - all in the sitemap.
- **The domain is new** (registered 14 Nov). Some company web filters block "newly registered domains" for ~30-90 days
  (seen on a corporate network on 17 Nov). Nothing to fix on the site; it ages out, or the filter vendor can be asked to
  categorise it (Computers & Technology / Software). Worth submitting to the big categorisation services early
  (Palo Alto, Zscaler, Cisco Talos, Fortinet, Symantec/Bluecoat site review pages).

## How the site is built and published
- Static pages in the repo's `docs/` folder, served by **GitHub Pages from `main` /docs**, custom domain via `docs/CNAME`
  (adeebometer.com, registered at Spaceship, DNS to GitHub Pages).
- Plain HTML + inline CSS, no build step, no framework. Keep it that way - pages are fast.
- `docs/sw.js` is an offline copy for the phone app. **After editing pages run `python3 tools/learnsite.py`** - it
  refreshes `sw.js` (and the Learn index); otherwise returning visitors keep seeing the old page.
- Work on a branch (the app's next version is `dev-1.1.6`); the owner merges site changes to `main`.

## Pages (17 Nov 2026)
| Page | Title | Sitemap | Description | og:image |
|---|---|---|---|---|
| `/` | AdeeboMeter — free Windows network troubleshooter (no admin, no cloud) | ✓ | ✓ | ✓ |
| `/app.html` | AdeeboMeter - your network, devices and PC in your pocket | ✓ | ✓ | **missing** |
| `/live.html` | Live: markets, scores, weather and your connection \| AdeeboMeter | ✓ | ✓ | **missing** |
| `/learn/` + 90 lessons | Learn how your internet works \| AdeeboMeter | ✓ | ✓ | **missing** |
| `/games.html` | Network games - quiz, subnet sprint, ports \| AdeeboMeter | ✓ | ✓ | **missing** |
| `/files.html` | Lock and open .adeebo files \| AdeeboMeter | ✓ | ✓ | **missing** |
| `/gaming.html` | AdeeboMeter for gaming - whose lag is it? | ✓ | ✓ | ✓ |
| `/streaming.html` | AdeeboMeter for streaming - why did it buffer? | ✓ | ✓ | ✓ |
| `/trading.html` | AdeeboMeter for trading - is it your connection or the broker? | ✓ | ✓ | ✓ |
| `/hall-of-fame.html` | Hall of Fame — AdeeboMeter | ✓ | ✓ | ✓ |
| `/privacy.html` | AdeeboMeter Privacy Policy | ✓ | ✓ | missing (fine) |
| `/AdeeboMeter-Theory-of-Everything.pdf` | the plain-language explainer | ✓ | - | - |
| `/r.html` | phone result page for QR links | **✗ on purpose** | - | `noindex` - keep it out |

`robots.txt` allows all and points to `/sitemap.xml`. Home has JSON-LD `SoftwareApplication` (version 1.1.5, `installUrl`
and `sameAs` to the Store). Microsoft Store id **9PFGXJPL9CLJ** (`https://apps.microsoft.com/detail/9PFGXJPL9CLJ`).

## What the product is (for copy and keywords)
A free Windows app (Microsoft Store or a signed download), local-first, no admin rights, no account. It tells you in one
sentence whether your PC, Wi-Fi, router, provider, DNS or the internet is the problem - then **shows it as a picture you can
send** (to your ISP, a friend, your phone). 158 tools by question ("Can I reach it?", "Why is it slow?", "Where does it
break?", "Is it this PC?"...). Modes for gaming (whose lag is it), streaming (why did it buffer), calls, trading (broker
latency). **Your phone**: link it with a QR code and see the PC's pages and run checks from anywhere, encrypted end to end
(matching words protect the pairing). The website also works on its own as a phone app: phone checks, Learn, Games, Live.

## Keyword angles (real intent, matches real features)
- Core: "why is my internet slow", "why is my PC slow", "is it my wifi or my internet", "is it me or my ISP"
  -> home headline now targets these directly.
- "how to prove to my ISP the problem is on their side" / "ISP report packet loss" -> the visual ISP report.
- "game lag wifi or server" / "whose lag is it" -> `/gaming.html`.
- "why does my stream buffer" / "OBS dropped frames network" -> `/streaming.html`.
- "broker latency check" / "is my connection fast enough to trade" -> `/trading.html`.
- Learn long tail (91 pages): "what is DNS", "what is MTU", "svchost what is it", "is msmpeng a virus", "what is lsass",
  "what is bufferbloat"... - each lesson is a small, honest answer page. Good for featured snippets.
- "network troubleshooter no admin rights" (work PCs), "free network tool windows signed".
- "subnet quiz" / "port numbers game" -> `/games.html`.

## Suggested next steps (owner decides)
1. **Titles and social previews**: the home `<title>` is fine; the home **og:title still says "Is it me or the
   network?"** - owner to decide whether it becomes the new headline/motto. Give app/live/learn/games/files an og:image
   (1200x630) each - shared links currently show no picture.
2. **Learn**: add `Article` (or `LearningResource`) JSON-LD per lesson and an FAQ-style "In one line" answer at the top -
   the lessons are already short, so this is mostly markup.
3. **FAQPage** JSON-LD on home, gaming, streaming, trading (real questions, short answers).
4. **Bing Webmaster Tools** (import from Search Console) - Windows users and the Store are Microsoft's.
5. **Search Console**: resubmit the sitemap (it grew from 7 to ~100 URLs on 16-17 Nov) and request indexing for `/learn/`,
   `/app.html`, `/live.html`.
6. Screenshots with descriptive `alt` on each page (the app's Save picture makes them; ask the owner).
7. `lastmod` in the sitemap when a page changes (`tools/learnsite.py` writes the Learn ones).
8. Keep Core Web Vitals green: no new scripts; images sized and lazy-loaded; videos already play from the site with
   YouTube only on click.

## Don't
- Don't add analytics, chat widgets, cookie banners or ad scripts.
- Don't index `r.html`, `HANDOVER-*`, `PLAN-*` or any `.md` file.
- Don't change privacy wording without the owner - it must match the app and `/privacy.html`.
- Don't promise anything the app doesn't do today (e.g. no "fixes your internet" - it finds, explains and shows).
