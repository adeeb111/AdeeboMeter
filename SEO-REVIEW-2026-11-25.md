# SEO review — 25 November 2026

Keep the current site design. This pass changes metadata and sitemap generation; new content should use the existing components and translation table.

## Measurement snapshot

- GitHub: 72 app-file download events across eight releases; 4 for 1.1.6. Counts include win64 ZIP and EXE only, excluding source and checksums. Repeat and owner/test downloads are included. Microsoft Store acquisitions are not yet verified.
- Cloudflare, last 24 hours, EST, bots excluded: 42 visits, 51 page views. Referrers: direct/no referrer 31, Facebook 8, Instagram 3. Device types: mobile 37, desktop 5. Visits are not unique people or installations.
- Google, selected 28-day Web report, available dates 14–23 November: 84 impressions, 3 clicks, 3.6% CTR, average position 27.1. Prior snapshot: 17 impressions and 2 clicks through 20 November. Different reporting cutoffs and a small sample: do not treat the average-position change as proof of a ranking loss.
- Google rolling 24 hours, 24–25 November: 40 impressions, 0 clicks; preliminary and overlapping the longer report, so do not add these figures together.
- Bing, 24 August–23 November: 0 impressions and 0 clicks.
- Sitemap resubmitted to both engines on 25 November. Google accepted; Bing processing. Both previously reported 453 discovered URLs, not the current 553. Submission is not indexing.

## Source inventory

553 unique sitemap URLs, all resolve to files locally: English 116, Bengali 110, French 109, Spanish 109, Arabic 109. This includes the PDF. There are 98 Learn lessons plus their index per language.

The handover's claim that every page has five copies is incomplete. Lab, Hall of Fame, phone app, Files, Live and Games lack localized static copies. Do not advertise nonexistent translated routes or add hreflang links to them. The older Bengali video-calls guide also needs a separate parity review.

## This branch

- Home SoftwareApplication metadata links the Store, official YouTube and Facebook profiles, and the direct 1.1.6 ZIP.
- Lab canonical comes from its source template.
- Sitemap dates for the redesigned core pages and PDF reflect the 24 November content release; home and Lab metadata changes use 25 November. Generated translations inherit English dates.
- Rebuilding Learn preserves existing content dates rather than declaring every page newly modified. When lesson content changes, update its reviewed sitemap date explicitly.
- Locale link generation caches repeated lookups within each run; generated language annotations remain the same.

## Next content pass

1. Review performance by language folder and query as more impressions arrive.
2. Prepare AdeeboShell and AdeeboLab guides using the existing layout, with reviewed translations before publishing.
3. Reconcile the missing localized utility pages with the translation session.
4. Review FAQ markup only where real questions and answers are visible; do not promise FAQ rich results.
5. After the owner merges, verify live metadata and request indexing for the four troubleshooting guides and three feature pages.

No Emil design-principles skill/file was found in the installed skills or this repository. The shipped design is the reference. No automatic merge or deployment.

## Content and language expansion — 25 November

Prepared on codex/shell-lab-guides; owner merge required.

- Two new English source guides: /adeeboshell.html and /adeebolab-guide.html. Uses guide.css, the existing site bar, flat coloured command rows and the five-stop diagram. Both have French, Spanish, Arabic and Bengali copies generated from the shared translation table, with localized titles, descriptions, canonical URLs, hreflang and TechArticle metadata.
- Five missing utility page families added to the language generator: phone app, Files, Live, Games and Hall of Fame. Twenty new language copies; shared scripts and cryptography remain unchanged in generated copies. Root data-file paths and dynamic lesson links work from language folders. The phone app keeps its full-screen layout and the embedded viewer retains its language.
- AdeeboLab’s interactive simulator remains the existing English tool. Its five-language guide explicitly discloses English simulator labels and explanations. Full simulator narrative translation belongs to the product/content translation pass; do not invent localized simulator URLs or claim that its UI is translated.
- Sitemap expected total: 583 unique URLs (553 previous + 10 guide pages + 20 utility copies). Updated content dates; private relay/result routes remain excluded.
- MTR full tutorial: https://www.youtube.com/watch?v=5IEcPIedL2I — How to Read MTR Results on Windows | Packet Loss Explained, published25Nov,2:22,Englishaudio. The visible description says the walkthrough uses the built-in sample; preserve that disclosure.
- MTR Short: https://www.youtube.com/shorts/L8fN2w5TXWQ — One Hop Shows Packet Loss. Is It Really a Problem? Facebook supplied share link resolves to https://www.facebook.com/reel/1128060556600411. Facebook body could not be read reliably; link retained as provided by owner, no Facebook metadata edits claimed.
- Full video, Short and reel linked from /learn/icmp.html (the existing ping/traceroute lesson) and /isp-report.html in five languages: ten localized cards. No YouTube/Facebook iframe, script or thumbnail before a click. CreativeWork subjects link to external watch pages; these articles are not video watch pages. Platform titles and chapters preserved.
- Final offline-cache refresh now runs after all language copies and site bars, so new Bengali pages are included and the hash describes the shipped HTML.
- New English copy is stored in the English pages; translations live in docs/locale-messages.js. Do not hand-edit generated language HTML.

After merge: verify the new live URLs, resubmit the 583-URL sitemap, and request indexing for the two new guides. Search discovery is not a guarantee of indexing or rankings.
