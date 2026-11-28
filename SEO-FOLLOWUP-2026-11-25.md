# SEO follow-up — 25 November 2026

PR44 is deployed. All 30 new pages were fetched successfully with correct languages and self-canonicals. All ten localized ICMP/ISP-report pages contain the three MTR video links. The live sitemap has 583 URLs; Google reports Success and 583 discovered pages. Both English guides received accepted priority-crawl requests. Bing accepted all ten localized guide URLs; its expanded sitemap is processing after the prior 553-URL version completed successfully.

## This follow-up

Four visible questions across the Shell and Lab guides, using existing expandable sections. Answers describe existing behavior. Translations live in locale-messages.js. FAQ metadata is generated from the actual rendered questions and answers in each language, so it cannot silently retain the English answers on translated pages. Rebuild through the existing locale generators; do not edit generated copies.

This markup describes the page content. It does not promise a Google enhancement: Google's June 15, 2026 documentation update says FAQ rich results are no longer shown (https://developers.google.com/search/updates).

Translation consistency review: brand names, command tokens, file paths and English simulator button names remain recognizable. This is a source and language consistency review, not independent native-speaker approval. Editorial approval remains with the owner's translation session.

## Translation-session handover

Owner explicitly assigned interactive Lab translation to the separate translation session. Maintain tools/lab/lab.src.html and tools/lab/build.py; docs/lab.html is generated and also shipped inside the Windows app. Translate controls, menus, examples, provider-equipment explanations and fault narratives together. Preserve node IDs, file formats, keyboard shortcuts, simulation behavior and the no-real-traffic distinction. Review Arabic direction and drawing labels without mirroring packet topology. Do not advertise localized simulator routes before they exist. The guides currently disclose that simulator labels and explanations are English; update that disclosure only when translations actually ship.

## Measurement follow-up

No repeat submission is needed immediately after the accepted requests. Compare the seven original priority pages and the ten new guides by engine, language, indexed status and crawl date. Keep sitemap discovery, submission acceptance and actual indexing separate. Compare search metrics over matching reporting windows; the current small sample cannot establish a ranking or conversion trend. Microsoft Store acquisitions remain unverified. No scheduled automation was requested or created.

## Homepage tutorial replacement

Owner supplied https://studio.youtube.com/video/soEsOAInV-U/edit and authorized replacing the home video. Public YouTube metadata verified: “Is It Your Wi-Fi or Your Internet Provider? Check on Windows”, 81 seconds, published November 25. Description already explains the two round trips, Wi-Fi/Ethernet comparison, observed results, AdeeboShell status and nine chapters. Preserve this accurate title and chapter timing. Studio could not be opened through the browser connection; no platform metadata changes are claimed.

Matching local source: content workspace's Wi-Fi or Your Internet Provider - 1080p Review.mp4, 81.32 seconds, 1920×1080 H.264/AAC, 6,885,481 bytes. Opening frame visually checked. Replaces only the short trailer across five homepages; the full tour remains. Self-hosted MP4 and extracted local poster, controls and preload=none, no autoplay. No embedded third-party player or remote thumbnail. New local video metadata describes English audio and the real duration; homepage remains a general product page, so no Google video rich-result eligibility is claimed. The matching Wi-Fi guide gains a localized watch card in all five languages.

Suggested YouTube description addition when Studio is accessible: “Follow the Wi-Fi vs Ethernet guide: https://adeebometer.com/wifi-or-isp.html”. Keep the measured comparison and existing chapter timestamps. Tags, if needed: Wi-Fi troubleshooting, Wi-Fi vs Ethernet, slow internet Windows, router latency, internet provider, AdeeboMeter, AdeeboShell. Do not describe the 20 ms internet reading as proof of an ISP fault.
