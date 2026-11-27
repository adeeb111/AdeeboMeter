# Five scenario articles — 27 November 2026

English articles, using the existing flat guide layout and brand colors:

- `/blog/fast-speed-test-buffering.html`: speed-test snapshots versus sustained playback.
- `/blog/game-lag-or-low-fps.html`: rendering problems versus network symptoms.
- `/blog/internet-slow-every-evening.html`: repeatable comparisons across times of day.
- `/blog/wifi-disconnects-ethernet-works.html`: Wi-Fi association versus upstream connectivity.
- `/blog/mtr-one-hop-packet-loss.html`: intermediate probe replies versus destination results.

Each article contains a short answer, labeled explanatory visual, practical comparison, result-specific next steps, evidence summary and primary-source reading. Illustrations are explicitly examples, never customer measurements. The MTR article links the full tutorial, Short and Facebook clip; the Wi-Fi article links its tutorial. Videos remain external links.

## Discovery and maintenance

Five new cards lead the English blog index (11 total). Five existing feature guides link relevant stories. Each article has a self-canonical, English and x-default alternates, social metadata and BlogPosting data. The index ItemList follows its actual cards. The sitemap now contains 704 unique URLs.

`tools/scenario_blog.py` maintains cards, related links and sitemap entries; article HTML remains authored source. It runs in `tools/site.sh` before localization. Localization removes the marked English-only blocks so unreviewed articles are not advertised as translated content. The offline cache includes the new pages and stylesheet.

## Translation handover

Separate translation review owns Bengali, French, Spanish and Arabic versions. Translate the complete article, visual labels, source captions and metadata; check technical meaning before adding routes, reciprocal alternates, localized index cards and sitemap entries. Do not represent English videos as localized audio. Lab simulator translation stays with the translation session.

## Validation

- Generator rerun is byte-for-byte idempotent.
- All 704 unique sitemap destinations resolve to local files.
- Five article canonical URLs, English alternates, JSON-LD and local asset/navigation links checked.
- English index ItemList contains 11 items.
- Repository CI checks clean; diff whitespace check clean.
- Browser could not reach the localhost preview; rendered desktop/mobile visual verification remains outstanding. Responsive CSS uses stacked cards below 520px and a contained scrolling table.

## After owner merges

Verify the five production URLs return 200 and inspect their rendered mobile layouts. Submit the updated sitemap to Google and Bing, then request discovery of the five canonical article URLs where the tools allow. Record actual submission results separately; this PR does not claim indexing or rankings.

Conflict resolution: preserved main cb18c834 localization changes, Chinese build step, sitemap entries and offline assets. Both commit histories are retained. Cache version refreshed from the main cache plus changed article content; full build recomputes the standard full-content hash.
