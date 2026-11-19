# Spanish website/mobile completion — 19 November 2026

This supersedes the website gap recorded in SPANISH-WINDOWS-HANDOVER.md. Both catalogs are now filled: **3,738/3,738 web/mobile keys and 4,197/4,197 Windows keys**. The literal Windows checker covers 2,037/2,037 texts. No missing keys or placeholder, numeric-token or edge-whitespace issues in the shared audit.

## Changes

- Expanded Spanish web catalog from 73 to 3,738 entries: homepage, installation/policies, Theory of Everything slides/SVG labels, phone tools/results/settings, Learn lessons/diagrams/quizzes, games, Live markets/sports/weather, files/encryption, privacy, guides and blog text represented in the source inventory.
- Reused 1,334 exact/equivalent Windows translations or technical identifiers; translated missing prose and generated repeated diagram wrappers from translated actor/message keys. All indexed batches and the frozen missing inventory are retained in the parent workspace for corrections.
- Kept existing HTML/CSS layout. Spanish uses the shared full homepage, not a separate reduced landing. Existing top selector governs language and persists through blog/home navigation. No Hindi option added.
- Hide the generic preview warning for Spanish, matching other filled catalogs. Refreshed the service-worker cache hash so existing phone installations receive the new catalog.
- Product names, standards, commands, keyboard keys, IP addresses, units and pairing verification words intentionally retain their spelling. Those words must match the PC and should not be independently translated.
- No Windows source changes in the web pass. Previously built Spanish Windows executable remains the review build.

## Verification

`node tools/localization-audit.cjs es` passes both surfaces. The browser check passed homepage/policies; no horizontal overflow at widths 320, 390, 768, 1201 and 1440; English/Spanish restoration; saved Spanish through blog/home; Learn DHCP quiz; phone tools at 320, 390 and 1024; and Live weather controls. Browser page-error list was empty. Desktop homepage, phone tools and blog screenshots were visually reviewed.

Parent-workspace `check-web-locales.cjs` chooses this checkout while its index-es.html preview exists. `generate-spanish-previews.cjs` creates standalone Spanish home, phone, Live and blog HTML review copies. These generated previews are not production source and must not be committed to docs. `tools/localization-proof.cjs es` exports the full web + Windows bilingual TXT for native review. The combined change record includes previous and current values per surface.

The shared catalog is intentionally offline-capable and adds no translation service or runtime network calls. Catalog-size measurements are retained in the parent workspace. The site's existing styles and scripts remain shared across languages.

## Publication and review

User said to finish the rest before pushing. These changes are prepared locally for review; **no Spanish push/deployment has occurred**. Owner/native-speaker wording and a complete real-device walkthrough remain pending. Exact-key coverage cannot prove every arbitrary runtime phrase or external-service response is translated. The separate English Theory of Everything PDF and Windows buddy personality sources are outside these catalogs; HTML Theory slides are included.

Official Windows installer release still needs the release/provenance workflow and owner's Certum/SimplySign signing step. Never replace the signed public installer with the unsigned local review executable.
