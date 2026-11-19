# AdeeboMeter - content manager handover

For whoever creates and posts AdeeboMeter's content. Written 18 Nov 2026. The owner is Adeeb Khan: **he approves every
piece before it's published or posted.** Works alongside the SEO person (`SEO-HANDOVER.md`, `HANDOVER-SEO.md`), the
translator/localization (`HANDOVER-TRANSLATOR.md`, `HANDOVER-LOCALIZATION.md`) and the UI/UX designer (`HANDOVER-UIUX.md`).

## Who does what
```
SEO        → which questions people search (topics, keywords, what's ranking)  ─┐
Content    → writes the English piece, makes the visuals, drafts the social posts ├─► Adeeb approves ─► published
Translator → French (Canada) and Bangla versions of approved pages               ─┘
```
English comes first; translations follow the approved English, never the other way round.

## What AdeeboMeter is (the facts to write from)
- A **free Windows app** (Microsoft Store or a code-signed download from adeebometer.com). No account, no admin rights,
  works on locked-down work PCs.
- It tells you **in one sentence** whether the problem is your PC, Wi-Fi, router, provider, DNS or the internet - and
  turns it into **a picture you can send** (to your ISP, IT, a friend, your phone).
- 158 tools grouped by question ("Can I reach it?", "Why is it slow?", "Where does it break?", "Is it this PC?").
  Modes for **gaming** (whose lag is it), **streaming** (why did it buffer), **calls**, **trading** (broker latency).
- **Your phone:** link it with a QR code; see the PC's pages and run checks from anywhere, encrypted end to end (matching
  words keep strangers out). The website also works as a phone app on its own: phone checks, Learn, Games, Live.
- A friendly **buddy** with moods and personas - a companion, not a robot.
- **Private by design:** the app has no telemetry, no cloud, never listens for connections. (The website counts visits
  with Cloudflare Web Analytics - cookieless, honours Do Not Track - see `/privacy.html`.)
- Motto: **"Know what's wrong. Show anyone."** Website headline: **"Find out why your internet or PC is slow. And much more."**

## Where content lives
| Kind | Where | Format |
|---|---|---|
| **Troubleshooting guides** (blog) | `docs/<slug>.html` at the site root, listed on `/blog/` | Static HTML using `guide.css` - copy `docs/wifi-or-isp.html` |
| **Learn lessons** (short reference) | `knowledge/learn.txt` -> `python3 tools/learnsite.py` builds `/learn/` | Two lines + a picture; ask the developer to add |
| Mode pages | `gaming.html`, `streaming.html`, `trading.html` | Edit only with the SEO person |
| Videos | YouTube (trailer + tour exist; linked from the home page) | |
| Social | LinkedIn first (Adeeb's audience), then others he chooses | Drafts for him to post |

**A guide follows the existing shape** (see `wifi-or-isp.html`): the question as the title -> **the short answer** first
("Try Ethernet first.") -> **Run the checks** (numbered, concrete) -> **What the readings mean** -> **Worked example** (a real
result picture) -> **Use AdeeboMeter on Windows** (one short section, not a sales pitch) -> **What to keep** (2-3 lines).
Then add a card to `/blog/`, the page to `sitemap.xml`, and run `python3 tools/learnsite.py` (refreshes the offline copy).

## Voice
- **Short and plain.** One idea per sentence. People arrive annoyed because something is broken - answer first, explain after.
- **Friendly, never cringe, never hype.** No "revolutionary", "ultimate", "game-changer". The buddy can be funny; guides
  stay calm and useful.
- **True today.** Every claim must match the released app (1.1.5 now). Don't promise features from plans.
- **Say "your provider" or "your ISP"**, never name and shame a real company.
- Spell out a term the first time (DNS - the internet's phone book), then use it.

## Visuals
- Use **real results**: the app's **Save picture** makes clean result images (path picture, ISP report). Ask Adeeb or the
  developer for a set; never mock up fake numbers as if they were real.
- Flat, the AdeeboMeter colours (navy, blue, green / amber / red for fine / warning / problem), **2D icons, no emoji**.
- The logo is fixed (blue rounded-square gauge over a three-dot line; **Adeebo**Meter wordmark). Get it from
  `docs/img/logo.svg` - don't redraw, recolour or stretch it.
- Every image gets descriptive `alt` text and is sized (width/height) so pages stay fast.

## Rules (hard)
1. **No employer or company names** connected to Adeeb, anywhere - posts included.
2. **No fake reviews, testimonials or quotes.** Real user feedback only, with their permission.
3. **Investing content is education only** - no tips, no "buy/sell", no real tickers as recommendations.
4. **No new trackers, pixels or embeds** that load third-party scripts (YouTube plays only on click - keep it that way).
   The only analytics is Cloudflare Web Analytics, already set up.
5. **Don't mention GitHub** on the website outside the About/source link (Adeeb's friends found it confusing). Social posts
   can link adeebometer.com.
6. **Work on a branch and open a pull request**; Adeeb merges. Nothing goes to `main` directly.
7. Don't change `/privacy.html` wording without Adeeb.

## Topic ideas (match real features and real searches)
Ask the SEO person which have demand first.
- "My game lags - is it my internet or the game server?" (Gamer mode, whose lag is it)
- "Why does my stream buffer when my speed test is fine?" (streaming, Wi-Fi dips)
- "Video calls drop or freeze - what to check" (calls)
- "How to prove to your ISP the problem is on their side" (the ISP report picture)
- "Is hotel / airport Wi-Fi safe to use?" (Wi-Fi safety check)
- "Is my VPN leaking?" / "Is my port open from outside?"
- "My PC is slow - what is this process?" (Learn already covers svchost, MsMpEng, lsass...)
- "What does a code-signed app mean, and why it matters"
- "Control your PC's network checks from your phone" (the phone link, matching words)
- Short "Did you know" posts from Learn lessons (91 of them), one a week

## A simple rhythm (adjust with Adeeb)
- **Weekly:** one LinkedIn post (a tip with a real result picture), one short Learn-based post.
- **Every two weeks:** one new guide on the blog, from the SEO list.
- **Each release:** a "what's new" post with 3 real screenshots (Adeeb shares the release notes).
- Monthly: look at Cloudflare Web Analytics (visits per page) with the SEO person; write more of what people read.

## Handing a piece to Adeeb
Send: the page or post text, the images, the target keyword (from SEO), and one line on why this topic now.
He approves or edits; then the PR is merged (website) or he posts (social).
