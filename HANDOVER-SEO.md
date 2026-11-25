# AdeeboMeter website - SEO handover

For whoever handles SEO for **https://adeebometer.com**. Updated 24 Nov 2026, the day 1.1.6 shipped (earlier versions
15 and 17 Nov). Read the rules first - they are the owner's, not suggestions. Works alongside the content creators
(`HANDOVER-CONTENT.md`).

## Rules (hard)
1. **No company/employer references anywhere** (no employer or vendor names, and none of their product names - see the
   private brief). Generic wording only.
2. **No tracking**: no Google Analytics, tag managers, cookies, pixels or third-party scripts that phone home. The only
   measurement is **Cloudflare Web Analytics** (cookieless, already set up). Search Console / Bing Webmaster are fine.
3. **No GitHub for visitors.** Since 24 Nov the site sends nobody to GitHub: downloads open a download window that
   fetches the zip directly; "What's new" and the MIT License open plain-words windows. GitHub is named only where legal
   or policy text needs it (the code-signing policy, the privacy page's "the source is public" line). Keep it that way.
4. **Never publish** or merge to `main` yourself - prepare a branch and the owner merges.
5. **Investing pages are education only** - no advice, no tips, no tickers as recommendations.
6. **Copy rules:** short, plain, friendly; no hype; every claim must be true of the released app (1.1.6).
7. Motto: **"Know what's wrong. Show anyone."** Headline: **"Find out why your internet or PC is slow. And much more."**
8. **Every page exists in five languages** (en, fr, es, ar, bn) and must stay the same page in each - change English,
   then the language copies are rebuilt from it (see "How the site is built").

## What changed this week (18-24 Nov)
- **1.1.6 released 24 Nov** - GitHub (code-signed) and the **Microsoft Store** (published the same night, new listing text
  in all five languages). 232 tools in six sections, five languages, AdeeboShell (the app from the keyboard), AdeeboLab
  (draw a network, break it), companies on the Markets page, a new one-page ISP report, a calmer corner widget.
- **Five languages on the website**: `/fr/`, `/es/`, `/ar/` (right to left), `/bn/` copies of every page, with `hreflang`
  links and a language picker at the top of every page. The brand name stays "AdeeboMeter" in every language.
- **New look** across the whole site (blue faceplate from the logo, Barlow type), and **one site bar on every page**
  (Home, Learn, Blog, Games, Live, More, day/night, Download).
- **Blog and the four guides rewritten** around the same "five stops" picture (PC, Wi-Fi, Router, Provider, Internet).
- **Feature pages** (`/gaming.html`, `/streaming.html`, `/trading.html`) now show the real corner widget and the real full
  window (PNG screenshots, also their `og:image`).
- **Privacy page rewritten** (and its translations restored on 24 Nov after a regression).
- **The poster**: `/img/poster.png` (2160x2700) - first-time visitors see a small corner card with it for 8 seconds,
  once; it's also under More > Poster.
- **Social channels**: YouTube `@AdeeboMeter` (https://www.youtube.com/@AdeeboMeter) and Facebook
  (https://www.facebook.com/adeebometer). Add them to the home page's JSON-LD `sameAs` (see next steps).
- **AdeeboLab** page `/lab.html` is in the sitemap (it has no site bar on purpose - it's a full-screen tool).

## How the site is built and published
- Static pages in the repo's `docs/` folder, served by **GitHub Pages from `main`**, custom domain via `docs/CNAME`.
- Plain HTML + CSS, no framework. Keep it that way - pages are fast.
- **The English page is the source.** After editing, run `sh tools/site.sh`: it rebuilds the Lab and Learn pages, the
  offline copy (`docs/sw.js`), then the fr/es/ar/bn copies of every page from English (through the translation table
  `docs/locale-messages.js`), then the site bar. **New English text needs its translations added to
  `locale-messages.js`**, or the language copies fall back to English - that is exactly what broke the privacy pages on
  24 Nov. `python3 tools/site_sync.py --check` and `python3 tools/ci_checks.py` must both be clean.
- Work on a branch; the owner merges to `main`, which publishes.

## Pages (24 Nov 2026)
| Page | Purpose | og:image |
|---|---|---|
| `/` | home: headline, five stops, widget, download | ✓ |
| `/blog/` + 4 guides (`wifi-or-isp`, `internet-speed-monitor`, `pc-monitoring`, `isp-report`) | answer pages for the big questions | ✓ |
| `/gaming.html`, `/streaming.html`, `/trading.html` | modes, with the real widget and full window | ✓ (`img/*.png`) |
| `/app.html` | the phone app | ✓ |
| `/live.html`, `/games.html`, `/files.html` | Live data, network games, .adeebo files | ✓ / check |
| `/learn/` + 98 lessons | short honest answers ("what is DNS") | ✓ |
| `/lab.html` | AdeeboLab in the browser | - |
| `/hall-of-fame.html`, `/privacy.html` | | |
| `/AdeeboMeter-Theory-of-Everything.pdf` | the plain-language explainer (32 pages, 1.1.6) | - |
| `/r.html`, `/remote.html` | phone result / relay pages - **kept out of the sitemap on purpose** | - |

Sitemap: 553 URLs. Home JSON-LD `SoftwareApplication` says version **1.1.6**. Microsoft Store id **9PFGXJPL9CLJ**.

## What the product is (for copy and keywords)
A free Windows app (Microsoft Store or a code-signed download), local-first, no admin rights, no account, in five
languages. A faded widget in the corner shows five dots from the PC to the internet; the first one that isn't green is
where the trouble starts, and it says the fix in plain words. The full window goes deeper: What's wrong? (layer by
layer), **232 tools** found by typing, a one-page **Report for my ISP** with a message ready to paste, Gamer / Streaming /
Markets pages, Time machine (replay any minute), **AdeeboShell** (the app from the keyboard, Linux style, fenced),
**AdeeboLab** (draw your network, break one piece, watch where it fails), and the phone link (QR code, end-to-end
encrypted). No cloud, no telemetry, never listens.

## Keyword angles (real intent, matches real features)
- Core: "why is my internet slow", "why is my PC slow", "is it my wifi or my internet", "is it me or my ISP".
- "how to prove to my ISP the problem is on their side" / "packet loss report for ISP" -> `/isp-report.html`.
- "game lag wifi or server" / "whose lag is it" -> `/gaming.html`. "why does my stream buffer" -> `/streaming.html`.
- "broker latency check" / "follow stocks on desktop widget" -> `/trading.html`.
- New this week: "network simulator in the browser" / "bufferbloat explained" / "TCP CUBIC vs Reno" -> `/lab.html`;
  "linux commands on windows network tool" -> AdeeboShell (no page yet - a guide candidate).
- Language markets: French (Canada), Spanish, Arabic, Bengali versions of the same intents - check Search Console per
  language folder before writing new language-specific copy.
- Learn long tail (98 pages): "what is DNS", "what is MTU", "svchost what is it", "is msmpeng a virus"...

## Next steps (owner decides)
1. **Sitemap `lastmod`**: the pages rewritten this week still show 19-21 Nov (home, blog, the guides, gaming/streaming/
   trading, privacy, Hall of Fame) - bump them to 24 Nov so Google recrawls.
2. **Search Console + Bing**: resubmit the sitemap (now 553 URLs with the language folders); check the `hreflang`
   report for errors; request indexing for the four guides and the feature pages.
3. **JSON-LD `sameAs`**: add the YouTube and Facebook URLs next to the Store.
4. **Naming consistency** (fr/ar/bn): the home page's More menu and the site bar call some pages by different names
   (e.g. fr "Tableau d'honneur" vs "Temple de la renommée") - pick one per language.
5. **Home "What's new" tiles**: only the Gamer tile has a picture; the owner is deciding whether all five get one.
6. **FAQPage** JSON-LD on home and the feature pages (real questions, short answers).
7. A guide for AdeeboShell and one for AdeeboLab (both new, both searchable topics).
8. Video: the new per-tool videos (see `HANDOVER-CONTENT.md`) can each be embedded on the matching Learn or guide page
   - click-to-play only, as the home page does.
9. Keep Core Web Vitals green: no new scripts; images sized; videos load YouTube only on click.

## Don't
- Don't add analytics, chat widgets, cookie banners or ad scripts.
- Don't index `r.html`, `remote.html`, `HANDOVER-*`, `PLAN-*` or any `.md` file.
- Don't edit the fr/es/ar/bn pages by hand - they are rebuilt from English; fix the translation table instead.
- Don't change privacy wording without the owner - it must match the app and `/privacy.html`.
- Don't promise anything the app doesn't do today (it finds, explains and shows - it doesn't "fix your internet").
