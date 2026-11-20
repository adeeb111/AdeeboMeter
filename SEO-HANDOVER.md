# AdeeboMeter SEO rollout record

Updated November 18, 2026. Keep this file as the handover for each website language. Record deployment, submission acceptance and observed indexing separately for each URL.

## English baseline

| Area | What to carry forward | Evidence or status |
| --- | --- | --- |
| Brand discovery | Use the AdeeboMeter spelling consistently in the title, page copy and external listings. Check the favicon and site name on branded results. | The owner's Google screenshot shows the official homepage first, a globe icon and the suggestion “audiometer”. The screenshot confirms visibility for that search at that time. |
| Product intent | Give each distinct search intent a page with a clear title, description and product action. | The English homepage, internet speed monitor, PC monitoring and ISP report are in the sitemap. |
| Troubleshooting | Use Blog for scenario-led articles and Learn for reference material. Provide a short answer, flat visuals where they explain the diagnosis, a concrete check and clear results. | The English Blog and Wi-Fi vs ISP guide were submitted in both dashboards. The video-call guide has a visual and color-coded checks in the repository. |
| Technical SEO | Use a stable public URL, language-specific title and description, self-canonical, reciprocal language links on equivalent pages, crawlable internal links, and sitemap entries. Inspect public HTML after deployment. | Repository source and sitemap provide the baseline. |
| Submission | Use Google Search Console URL Inspection and Bing Webmaster Tools URL Submission. Verify acceptance in each dashboard. | Previous submissions and the Bangla rollout below were observed in the dashboards. |
| Measurement | Record index coverage and search impressions and clicks for branded and non-branded queries by page and country. | Historical metrics and page-by-page indexing dates still need a dashboard export. |

## Repeat for every language

1. Inventory the English URLs for product, monitoring, reporting, Blog and Learn. Track which equivalents have approved copy and public URLs.
2. Use terms people search for in that language, especially for slow internet, internet speed monitoring, PC monitoring and Wi-Fi troubleshooting. Align the title, description, headings and visuals with the page's intent.
3. Publish static, crawlable localized content under a stable URL. Check the page language, self-canonical, reciprocal hreflang on equivalent pages, social preview, favicon, navigation and sitemap. Verify the actual deployed pages and sitemap.
4. Request indexing in Google Search Console and submit the URLs in Bing Webmaster Tools. Record the date, response and retry reason. Leave the index status pending until separately verified.
5. Check search visibility and performance by URL and language. Check brand spelling and favicon separately from keyword rankings.

## Language and submission log

| Language | URL or scope | Google | Bing | Next |
| --- | --- | --- | --- | --- |
| English | /blog/ and /wifi-or-isp.html | Prior indexing requests visible in Search Console | Both submissions visible | Check index coverage and performance |
| English | /internet-speed-monitor.html and /pc-monitoring.html | URL inspections visible in history | Both submissions visible | Confirm per-page Google submission and index status |
| Bangla | /bn/ | Accepted Nov 18 | Accepted Nov 18 | Check index status |
| Bangla | /bn/blog/ | Accepted Nov 18 | Accepted Nov 18 | Check index status |
| Bangla | /bn/wifi-or-isp.html | Daily quota reached Nov 18 | Accepted Nov 18 | Retry Google when quota resets |
| Bangla | Speed monitor, PC monitoring, ISP report, video-call guide and Learn | Pending localized URL inventory | Pending localized URL inventory | Work from approved translations and verify crawlable equivalents |
| French | Existing website translation | Verify URL coverage | Verify URL coverage | Apply the rollout steps to approved pages |
| Hindi and Spanish | Planned website SEO | Pending | Pending | Start with the approved localized pages |

Production sitemap is at https://adeebometer.com/sitemap.xml. Bangla homepage setup shipped in GitHub PR 19.

## Bengali expansion prepared November 18, 2026

The next PR adds the exact Bengali brand spelling আদীবওমিটার visibly, in Bengali page titles and in application alternateName metadata. The domain homepage also declares it as a WebSite alternateName.

New static Bengali pages cover internet speed monitoring, PC monitoring and ISP reports. All 90 Learn lessons and the Learn index are rendered from the existing reviewed Bengali dictionary. New short troubleshooting copy and eight connecting phrases were drafted during this SEO work and can be reviewed by the translation owner. Flat visual examples and color-coded result lists reuse the site's existing design.

Run tools/learnsite.py to regenerate lessons. It invokes tools/bengali-seo.py to rebuild Bengali copies, language annotations and sitemap entries before rebuilding the offline cache.

These additional URLs require deployment verification and search-engine submission after merge. Google reached its daily manual submission quota on November 18. Accepted submission requests do not establish indexing or search position.

### Verification

All 98 Bengali pages passed browser checks for Bengali language, Bengali titles, self-canonicals, a single Bengali language annotation and valid internal Bengali links. The homepage, blog, four guide types and a Learn lesson were also checked at 390 px and 1120 px without horizontal overflow. The Bengali speed guide was visually reviewed. Service-worker syntax and staged whitespace checks passed.

The video-call guide has a Bengali URL and self language annotation. Its English counterpart is available in the earlier English working branch and is absent from the main branch used for this PR, so reciprocal annotations can be added when that English page is deployed.

## Visitor analytics prepared November 18, 2026

Cloudflare Web Analytics is configured for adeebometer.com in the owner's existing Cloudflare account. The public collection token is stored in docs/analytics.js. The shared locale script loads it on public pages, and standalone public pages receive a direct loader through tools/web-analytics.py. tools/learnsite.py reapplies that loader during generation.

Measures page views, visits, referring sites and page performance. Both English and Bengali pages are covered. The loader skips non-production hostnames, Do Not Track, Global Privacy Control and the companion app, files, live, remote, sharing and games routes.

The website privacy page describes Cloudflare measurement. Browser checks intercepted the provider script to verify one load per eligible page without sending test visits. Runtime checks verified DNT/GPC and companion route exclusions.

Collection begins after PR merge and deployment. Use Cloudflare Web Analytics for visitor metrics and Search Console/Bing for search impressions, clicks and queries. Record the deployment date with the first reported totals.

## French SEO expansion prepared November 18, 2026

Prepared 101 static French pages under /fr/ using the approved French Canada website dictionary. Coverage includes the full homepage, troubleshooting blog, Wi-Fi, speed and PC monitoring, ISP reports, gaming, streaming, trading, privacy, all 90 Learn lessons and their index.

Each page has a French title and description, a self-canonical, matching English/Bengali/French language annotations where equivalents exist, crawlable links and analytics coverage. French and French Canada language annotations share the same French URL. The old query-only French homepage sitemap entry is replaced by /fr/. Existing ?lang=fr links route to the corresponding static French page and retain anchors.

French search snippets, eight connecting phrases, two short labels and the new website analytics privacy paragraphs were drafted during SEO work. The substantive product and lesson copy comes from the approved translation.

The French build reuses tools/bengali-seo.py through tools/french-seo.py. The normal tools/learnsite.py build regenerates both languages and restores analytics before calculating the offline cache version.

All 101 pages passed browser checks for document language, canonicals, French language links, internal links and representative desktop/mobile layouts. The mobile speed guide was visually reviewed. French generation was byte-stable across repeated bilingual generation. Final indexing submissions await merge and production verification.

French route checks passed for direct French visits with a saved Bengali preference, switching to English and back, old ?lang=fr links, anchors and localized Learn header navigation. Added a visible English link on French articles and lessons.

## Spanish SEO and redirect audit — November 19, 2026

Adds 101 static Spanish pages under /es/ from the approved Spanish website catalog: homepage, Blog, Wi-Fi, internet speed monitoring, PC monitoring, ISP reporting, gaming, streaming, trading, privacy, Learn index and all 90 lessons. New SEO descriptions, image labels and analytics privacy connecting text are in tools/spanish-seo.json for translation review. The product and substantive lesson copy reuse the approved catalog.

The normal tools/learnsite.py build now invokes tools/spanish-seo.py and updates Spanish offline assets. Equivalent English, Bengali, French and Spanish pages get reciprocal hreflang links, self canonicals and language-route metadata. Spanish software/site structured data uses the localized URL and language. The full homepage is generated from the current English source with the same styling and Spanish translations.

Restores stable /bn/, /fr/ and /es/ homepage routes instead of redirecting them to /?lang=…. Direct localized URLs retain their language; English and Automatic remain available through the picker. Explicit query language choices and anchors remain supported.

Google's Page with redirect report listed http://adeebometer.com/, http://www.adeebometer.com/ and https://www.adeebometer.com/. All three were verified to return HTTP 301 directly to https://adeebometer.com/. These are intentional domain/HTTPS consolidation redirects, so no Validate Fix action or removal of redirects is appropriate.

Deployment, production verification and Google/Bing submissions must follow merge. Local generation and checks do not establish live deployment, indexing or rankings. Spanish submissions are pending until the URLs are public.

Validation: all 101 Spanish pages passed browser language/title/description/canonical/internal-link checks. Equivalent pages passed reciprocal Spanish hreflang checks; sitemap contains 407 unique URLs. Representative Spanish, Bengali and French pages passed mobile/desktop checks at 390px and 1120px without horizontal overflow or unexpected localized-home redirects. Explicit query switching, anchors, Spanish browser auto-selection and direct Spanish visits with a saved Bengali choice passed. All 90 Spanish lesson schemas declare Spanish and localized URLs. JavaScript syntax and whitespace checks passed. The Spanish speed guide was visually reviewed.
