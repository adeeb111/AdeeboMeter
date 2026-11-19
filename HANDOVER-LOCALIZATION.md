# AdeeboMeter translation record

The current pass is Bengali (`bn`). Adeeb reviews the rendered HTML after the Bengali work is ready. Use the same workflow for the next language.

## Where changes go

| Surface | Source | How to check it |
|---|---|---|
| Website homepage, Theory slides, demos and feedback form | `docs/index.html` and `docs/locale-messages.js` | Switch languages without leaving the page. Check the long labels, cards, carousel and all three feedback modes. |
| Mobile web app and Tools | `docs/app.html` and `docs/locale-messages.js` | Check Home, Settings, PC setup, Bengali tool search, loading/error messages and tool results. |
| Learn | `docs/learn/*.html`, generator `tools/learnsite.py`, and the web catalog | Check the lesson index, progress, Bengali search, diagrams, quiz answers and previous/next links. |
| Live Data | `docs/live.html` and the web catalog | Check connection measurements, markets, scores and weather. Check loading, failure and empty states with sample data. |
| Windows app | `knowledge/lang-bn.txt` | Rebuild, select Bengali in Settings, restart, and check menus and result text. |
| Shared selector and rendering | `docs/locale.js`, `docs/locale-surfaces.js`, `docs/locale.css` | Check automatic and manual selection, restored selection, mobile and desktop layouts, and language switching in embedded pages. |

Web prose uses exact English lookup keys. Keep translated words and numbers in the display path, and keep IDs, protocol names, file extensions, keyboard shortcuts, and source data intact. Use the translated phrase itself for search as well as the original English term. Give translators enough context to choose the right word.

## Bengali wording decisions

| English | Bengali | Use |
|---|---|---|
| Router | রাউটার | Network equipment |
| Internet provider / ISP | ইন্টারনেট সেবাদাতা | Use সেবাদাতা alone when context is clear |
| Tool / Tools | টুল / টুলস | Short menus and labels |
| Connection | সংযোগ | Use সংযোগ বিচ্ছিন্ন for a dropped connection |
| Delay | বিলম্ব | Use দেরি in short conversational explanations |
| Jitter | জিটার | Explain as বিলম্বের ওঠানামা where space permits |
| Packet loss | প্যাকেট লস | Preserve the distinction from delay |
| Results | ফলাফল | Use পরীক্ষা for an individual check |
| Settings | সেটিংস | Preserve exact Windows menu names when giving an OS path |
| Save | সেভ করুন | Short buttons |
| Upload / Download | আপলোড / ডাউনলোড | Short buttons and measurements |

Use natural everyday Bengali, with polite verbs such as করুন and দেখুন. Keep AdeeboMeter and theme names as they appear in the product. Keep examples, figures, links and warnings factually equivalent to the source.

## Work recorded

- The first shipped pass added the language dropdown and the Canadian flag, shared the homepage layout across languages, and added the reviewed Windows UI files.
- The Bengali follow-up repaired the narrow header and overlapping hero cards, removed the Bengali preview notice, and localized the install, policies, feedback and mobile setup sections.
- The web pass wired Learn, Live Data and the homepage Theory walkthrough to the shared catalog. It added Bengali lesson titles, search terms and copy for the first group of lessons, plus common Live Data loading and error messages.
- The latest pass filled two audited batches of homepage and Theory copy. The catalog is the exact English-to-Bengali record. The text inventory below separates matched items from candidates that still need a decision.
- Remaining work includes most detailed lesson text, linked pages, dynamic mobile and Live Data messages, remaining Windows strings, and any Bengali versions of the downloadable guide. The rendered homepage walkthrough and the downloadable PDF are separate artifacts.

## Repeatable coverage checks

Run from the repository root:

```text
python tools/web-langcheck.py bn --out bn-web-coverage.tsv
python tools/langcheck.py bn
python tools/export-bengali-review.py --out AdeeboMeter-Bengali-translation-list.txt
```

The web TSV records the page, English source, existing translation and occurrence count. Work in batches by surface. Check repeated English keys for context before reusing a translation. Review unmatched brand names and protocol labels separately from copy that needs translating. The static audit does not see messages assembled by JavaScript, so exercise loading, empty, error and success screens separately.

When preparing the review, regenerate the service worker cache after changing any listed asset. Share direct HTML for the homepage, mobile app, Learn and Live Data. The review copy should open in Bengali even before a stored preference is applied. Check that the reviewed files and the tested files are the same before giving them to Adeeb.

### Learn protocol pass

Added 158 Bengali source entries across ARP, DHCP, TCP, UDP, TLS, HTTP, ICMP, IPv6, MTU, NAT, QUIC, and Wi-Fi. These cover the lesson explanations, diagram steps, quiz prompts, and choices. Shared rendering now translates generated previous/next links and browser titles. The Learn generator and generated quizzes use translated feedback when a locale provides it. Protocol names and numbers remain available to match examples.

### Native-speaker terminology corrections

- Use ব্যাকগ্রাউন্ড for software background activity, including ব্যাকগ্রাউন্ড চেক, ব্যাকগ্রাউন্ড জবস, and ব্যাকগ্রাউন্ড গেম পিং. Use ব্যাকগ্রাউন্ডে when it describes where a task runs.
- Use হ্যান্ডেল for a Windows handle and টাইমস্ট্যাম্প for a timestamp. Retain বিটম্যাপ and সাইন-ইন ব্রোকার.
- Use গোপনীয়তা for privacy policies and headings. In Windows Settings, use প্রাইভেসি ও অনলাইন অনুসন্ধান.

### Learn routing pass

Added 81 Bengali entries for 802.1X, BGP, QoS/DSCP, Path MTU and the return path. Preserved protocol names, example IP addresses, AS numbers and VLAN IDs.

### Learn: inside your provider

Added 77 Bengali entries covering the OSPF/IS-IS, MPLS, L3VPN/VRF, and multicast lessons. Checked the original protocol names, numbers, IP addresses, and packet labels.

### Learn: CDN and live sports

Added 39 Bengali entries for the CDN and live football lessons. Kept CDN, HLS, DASH, IPTV, HTTPS, the bandwidth units and the original timings.

### Mobile tool results

Added 40 catalog pairs for tool controls and first-party phone connection measurements. Templates preserve the measured values and support Bengali word order. Device names, DNS records and copied technical reports retain the source data.

### Learn: mobile networks

Added 123 Bengali entries across the seven lessons about RAN, 4G, 5G, joining a network, APN/DNN, bearers and NSA/SA. Protocol and interface identifiers, example IP addresses, and diagnostic log labels stay in English. The existing lesson rendering and quiz logic also applies to the next language.

### Learn: Windows startup and sign-in

Added 88 entries covering Windows startup and the svchost.exe, lsass.exe, csrss.exe, wininit.exe, and winlogon.exe lessons. Kept the executable names, PIN, Ctrl+Alt+Del, and Win+L in English. Used ব্যাকগ্রাউন্ড for software background activity.

- Added 40 Bengali entries for Learn lessons on dwm.exe, explorer.exe, and services.exe. Kept process names and exact Windows menu names.

- Added 248 Bengali entries for 17 Learn lessons covering Windows processes, Microsoft apps, encryption, keys, and signatures. Preserved executable names, protocol names and example values.

### Combined Bengali website pass

Added 274 exact-source entries for Learn, including investment education, certificate revocation, CDN and video playback. Added another 416 entries for the remaining Learn copy and the linked pages for Files, Games, Gaming, Hall of Fame, ISP reports, Privacy, Streaming and Trading. Those pages now read the shared language catalog. The exact English/Bengali pairs remain in the catalog and text export.

- Added 84 more catalog entries for mobile validation, DNS and speed results, and Live Data connection measurements. Added shared formatting for named fields and language synchronization between pages. Protocol names and raw hostnames, numbers and provider-supplied data remain intact.

- Added 33 more mobile entries for the remaining tool headings, action buttons, and examples shown in input fields. Dynamic result bodies for less common tools still need follow-up.

- Added 237 Bengali entries for mobile tool results and Live Data. Named placeholders cover website checks, currency and market summaries, sports results, weather readings, and several mobile controls. Preserve raw values and provider-supplied copy. Derive market state from values instead of parsing an English display string.

- Added 121 more entries for website status and ports, market and sports summaries, spam-list results, weekly history, certificate and Base64 decoding, email setup, registration lookups, and the mobile network-roast screen, share text, and image. The 358 additions in this pass brought the website and mobile review list to 3,157 translated pairs. Weather rerenders from the existing response when the language changes. The browser fixtures exercise website checks, weather, and market results without using live providers. The signal-journey report and paired Windows output still need a dedicated language pass.

- The screenshot review found that mobile redraws skipped catalog entries on unmarked UI. Added exact copy lookup to the detached tree before it is merged and replaced comparisons that serialized HTML with direct DOM equality checks. Added 127 Bengali entries for the six troubleshooting cards, guide steps, and Signal Journey findings, measurements and share report. Browser checks cover 320 px and 390 px layouts and verify that filtering keeps the search input focused. Verify paired-PC output with a real Windows connection during the walkthrough.
- Applied Bengali proofreader edits to web/mobile and Windows catalogs. Checked the Hall of Fame copy across its bold labels, kept punctuation that joins UI fragments, and preserved the day count prepended by the Windows history dialog.

## French completion pass

French (Canada) has 3599 web/mobile entries and 2794 Windows entries. Includes every Bengali catalog key, current troubleshooting guides, mobile details and Windows settings/help fragments. Existing French entries retained. The proof list is AdeeboMeter-French-translation-list.txt.

Hindi removed from language lists, web catalog, Windows resource and language test expectations. Stored Hindi settings fall back to English. Compiled French resources and fallback verified by reflection.

Browser checks passed at 320, 390, 1280, 1440 and 1536 px, including blog, Tools, a Learn quiz, Live Data, switching and offline loading. Feature cards stack on narrow phones; French secondary navigation links hide at intermediate desktop widths.

Windows release build passed with the existing warnings in FullView.Labs.cs and Qr.cs. Native Windows visual review and native-speaker proof review remain. Companion personality lines and provider-supplied content retain their English sources. Owner approved publication on November 18, 2026. French translation and Hindi removal pushed together, preserving the concurrent Bengali SEO rollout.
