# AdeeboMeter - content creators' handover

For whoever creates and posts AdeeboMeter's content: videos, posts, guides. Updated 24 Nov 2026, the day 1.1.6
shipped (first version 18 Nov). The owner is Adeeb Khan: **he approves every piece before it's published or posted.**
Works alongside the SEO person (`HANDOVER-SEO.md`), the translators (`HANDOVER-TRANSLATOR.md`, `HANDOVER-LOCALIZATION.md`)
and the UI/UX designer (`HANDOVER-UIUX.md`).

## Who does what
```
SEO        -> which questions people search (topics, keywords, what's ranking)  -+
Content    -> videos, posts, guides, visuals                                      +-> Adeeb approves -> published
Translator -> French (Canada), Spanish, Arabic and Bangla of approved pages       -+
```
English comes first; translations follow the approved English, never the other way round.

## What's new this week (18-24 Nov) - the story for this month
**AdeeboMeter 1.1.6 is out** (24 Nov) on the Microsoft Store and adeebometer.com, code-signed.
- **Five languages:** English, French, Spanish, Arabic (right to left) and Bengali - the app and the website.
- **A calmer corner widget:** brightens only when you rest the mouse on it, never takes your clicks, moves only when you
  ask (rest 2.5 s, or tray > Move widget). 13 widget styles, including Markets, Sports, Data use and Weather.
- **Markets:** follow companies (big names or search) and put up to four on the widget; prices from your own free key
  (Financial Modeling Prep, Finnhub or Twelve Data).
- **Report for my ISP**, redone: the answer and the five stops on page one, a message ready to paste, hop by hop.
- **AdeeboShell:** the whole app from the keyboard, Linux style - fenced so it can't touch the PC.
- **AdeeboLab:** draw a network, break one piece, watch where it fails (also in the browser: `/lab.html`).
- **232 tools** in six sections; the keyboard reaches everything; anything slow says what it's waiting on.
- **The website** got a new look, one bar on every page, a download window (no GitHub), rewritten blog and guides, real
  screenshots of the widget and the full window, and **the poster** (`docs/img/poster.png`, 2160x2700).
- **Channels:** YouTube https://www.youtube.com/@AdeeboMeter - Facebook https://www.facebook.com/adeebometer - and
  LinkedIn (Adeeb's own). Launch posts for Facebook and YouTube were drafted on 24 Nov; ask Adeeb for them.

## What AdeeboMeter is (the facts to write from)
- A **free Windows app** (Microsoft Store or a code-signed download from adeebometer.com). No account, no admin rights,
  works on locked-down work PCs. Windows 10 and 11.
- A small faded widget in the corner shows **five dots from the PC to the internet** (PC, Wi-Fi, Router, Provider,
  Internet). The first one that isn't green is where the trouble starts, and it says the fix **in one plain sentence**.
- The full window goes deeper: What's wrong? (layer by layer), 232 tools found by typing, the Report for my ISP, Gamer
  (whose lag is it), Streaming (why did it buffer), Markets (the line to your broker, the companies you follow), Time
  machine (replay any minute), AdeeboShell, AdeeboLab, Learn (98 short lessons).
- **Your phone:** link it with a QR code; see the PC's pages and run checks from anywhere, encrypted end to end.
- A friendly **buddy** with moods and personas - a companion, not a robot.
- **Private by design:** no telemetry, no cloud, never listens. (The website counts visits with Cloudflare Web
  Analytics - cookieless - see `/privacy.html`.)
- Motto: **"Know what's wrong. Show anyone."** Website headline: **"Find out why your internet or PC is slow. And much more."**

## Videos: a clip for every tool, and a combined demo (new, 24 Nov)
AdeeboMeter can tour itself for a screen recording. Adeeb has the test build (`AdeeboMeter-tour-test.zip`: the app,
`Record-Tour.ps1` and a READ ME); from 1.1.7 it's in the released app too.

```
Record-Tour.ps1  ->  you start OBS  ->  the app walks every tool:            ->  tour-chapters.csv
                                        open, Example inputs, run (or show       (when each tool started)
                                        its example), scroll, hold 5 s, next
Record-Tour.ps1 -Split <recording> -Chapters <csv>  ->  001-ping.mp4, 002-mtr.mp4 ...  +  youtube-chapters.txt
```
- **About an hour** for all 232 tools at the default 5 s hold. `-Hold 8` for a slower pace; `-From mtr -Count 1` to
  redo one tool.
- **Privacy - check every clip before it goes out.** The tour runs tools for real, so recordings show the recording
  PC's public IP, Wi-Fi name, router and device names. Record on a PC and network that are fine to show, or blur.
  Tools that send test traffic, touch files or need a password only show their Example result.
- **Hands off** the mouse and keyboard while it runs (a click changes the page); Esc stops it.
- **Per-tool videos (Shorts / Reels):** each clip is one tool from open to result. Add a title card with the tool's
  name and the one question it answers (the Toolbox groups tools by question - use that wording).
- **The combined demo:** the whole recording with `youtube-chapters.txt` pasted into the description, so viewers jump to
  a tool. Consider cutting a 3-5 minute highlights version: the widget, What's wrong?, Report for my ISP, AdeeboShell,
  AdeeboLab, Gamer.
- Record in the theme and language the video is for: the app follows its own setting (globe button for language).

## Where content lives
| Kind | Where | Format |
|---|---|---|
| **Troubleshooting guides** (blog) | `docs/<slug>.html`, listed on `/blog/` | Static HTML using `guide.css` - copy `docs/wifi-or-isp.html` |
| **Learn lessons** | `knowledge/learn.txt` -> built by `sh tools/site.sh` | Two lines + a picture; ask the developer |
| Mode pages | `gaming.html`, `streaming.html`, `trading.html` | Edit only with the SEO person |
| Poster | `docs/img/poster.png` (and a design canvas Adeeb owns) | 1080x1350 layout at 2x - for Facebook / Instagram |
| Videos | YouTube @AdeeboMeter (trailer and tour already linked from the home page) | |
| Social | Facebook page, YouTube community posts, LinkedIn | Drafts for Adeeb to post |

**A guide follows the existing shape** (see `wifi-or-isp.html`): the question as the title -> **the short answer** first ->
**Run the checks** (numbered) -> **What the readings mean** -> **Worked example** (a real result picture and the five-stop
strip) -> **Use AdeeboMeter on Windows** (one short section) -> **What to keep** (2-3 lines). Then a card on `/blog/`.
After any page change run `sh tools/site.sh` - it rebuilds the fr/es/ar/bn copies from English, so **new English text
needs translations** in `docs/locale-messages.js` (ask the translator), or those pages show English.

## Voice
- **Short and plain.** One idea per sentence. People arrive annoyed because something is broken - answer first.
- **Friendly, never cringe, never hype.** No "revolutionary", "ultimate", "game-changer".
- **True today.** Every claim must match the released app (1.1.6). Don't promise features from plans.
- **Say "your provider" or "your ISP"**, never name and shame a real company.
- Spell out a term the first time (DNS - the internet's phone book), then use it.

## Visuals
- **Real results only:** the tour recordings, the app's **Save picture**, and the screenshots in `docs/img/` (widget
  cards in `docs/img/widgets/` at 2x, full-window shots `gaming.png`, `streaming.png`, `trading.png`). Never mock up
  numbers as if they were real.
- Flat, the AdeeboMeter colours (the logo's blue; green / amber / red only for fine / warning / problem), Barlow type,
  **2D line icons, no emoji in the app or the website**. (A few emoji in social posts are fine.)
- The logo is fixed (blue rounded-square gauge over a three-dot line; **Adeebo**Meter wordmark) - take it from
  `docs/img/logo.svg`; don't redraw, recolour or stretch it.
- Alt text on every image; sized images so pages stay fast.

## Rules (hard)
1. **No employer or company names** connected to Adeeb, anywhere - posts included.
2. **No fake reviews, testimonials or quotes.** Real feedback only, with permission.
3. **Investing content is education only** - no tips, no "buy/sell", no tickers as recommendations.
4. **No new trackers, pixels or embeds** that load third-party scripts (YouTube plays only on click).
5. **No GitHub for visitors** on the website - downloads, What's new and the license all stay on the site. Posts link
   adeebometer.com or the Microsoft Store.
6. **Work on a branch and open a pull request**; Adeeb merges. Nothing goes to `main` directly.
7. Don't change `/privacy.html` wording without Adeeb.

## Topic ideas (match real features and real searches - ask SEO which have demand)
- "Is it my Wi-Fi or my internet?" - the five dots (widget clip + guide).
- "How to prove to your ISP the problem is on their side" - Report for my ISP (new one-page report).
- "My game lags - my internet or the server?" - Gamer.
- "Why does my stream buffer when my speed test is fine?" - Streaming.
- "Follow stock prices in a corner widget" - Markets companies (education only).
- "Linux commands for your Windows network" - AdeeboShell (new, no guide yet).
- "See a packet's path, then break it" - AdeeboLab (new; good for short animated clips).
- "232 tools, one a day" - the per-tool clips as a daily Shorts / Reels series.
- "Did you know" posts from Learn lessons (98), one a week.

## A simple rhythm (adjust with Adeeb)
- **Daily (while the clips last):** one per-tool Short / Reel with its one-line question.
- **Weekly:** one Facebook / LinkedIn post with a real result picture; one Learn "did you know".
- **Every two weeks:** one new guide from the SEO list.
- **Each release:** a "what's new" post and the combined demo re-recorded with the tour.
- Monthly: Cloudflare Web Analytics with the SEO person; make more of what people read and watch.

## Handing a piece to Adeeb
Send: the post or page text, the images or clip, the target keyword (from SEO), and one line on why this topic now.
He approves or edits; then the PR is merged (website) or he posts (social).
