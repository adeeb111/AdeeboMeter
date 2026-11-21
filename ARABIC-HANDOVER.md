# Arabic localization review — 19 November 2026

Adeeb authorized pushing this Arabic implementation on 19 November 2026. Native Arabic proof review remains pending.

## Coverage and proof files

- Website/mobile catalog: 3,738 entries, covering the 3,599-entry French reference plus 139 additional text nodes found in the English HTML, including diagram and privacy text. Product names, protocols, addresses, code and pairing words are intentionally kept where appropriate.
- Windows catalog: 4,197 entries. Includes all 2,037 translatable literals found by langcheck, existing variable-based labels, Learn fields, quiz choices and diagram messages. The collector excludes self-test sentinels and punctuation-only strings.
- The shared inventory contains 6,772 distinct English keys. The proof TXT contains a section for each surface. A separate review-status TXT identifies existing entries, machine drafts and entries with an AI editorial pass. A native Arabic speaker has not proofread the complete catalog; do not treat coverage as linguistic approval.
- Proof files in the parent workspace: `AdeeboMeter-Arabic-translation-list.txt`, `AdeeboMeter-Arabic-review-status.txt`, `Arabic-homepage-desktop.png`, `Arabic-homepage-phone.png`, `Arabic-mobile-tools.png`, `Arabic-Windows-text-preview.png`.
- Local HTML proofs: `docs/index-ar.html`, `docs/app-ar.html`, `docs/live-ar.html`, `docs/blog/index-ar.html`. They use the normal components and force Arabic for review. These standalone proof pages are not production routes.

## Implementation

- Arabic sets the document's RTL direction; switching back restores LTR. The full homepage and shared language picker remain in use. No simplified Arabic homepage redirect was added.
- RTL adjustments cover phone/desktop navigation, the app's side rail, input alignment, timelines, card labels and navigation chevrons. Code, addresses and technical diagrams retain their technical order.
- SVG text labels now participate in the existing translation pass, so the Theory of Everything diagrams can translate alongside ordinary page text.
- Windows uses Arabic-capable Segoe UI fonts at startup and at alternate zoom levels. Common headings, tiles, settings and button text support RTL. Tray menu captions, static UI captions and shared explanatory-text helpers now use the language catalog.
- Learn translates content before measuring paragraphs, and translates ladder labels after parsing the English diagram syntax. Structural separators are not translated before parsing.
- No translation model, package or network translation dependency is included in the app or website. The local drafting tools and weights stay outside the source tree.

## Reuse for the next language

1. Use the exact English keys, including edge spaces, from the shared inventory and existing catalogs; keep variable-based entries even when the literal collector does not list them.
2. Preserve `{placeholders}`, numbers, units, product names, commands, file extensions and code. Check syntax-bearing diagram strings separately.
3. Run `node tools/localization-audit.cjs <code>` and `python tools/langcheck.py <code>`. Do not translate self-test fallback sentinels.
4. Check actual rendered pages and dynamic app views, then the Windows resource table and fonts. Inspect phone and desktop screenshots; coverage alone does not detect bad wording or a missing runtime lookup.
5. Record native proofreader corrections separately from machine drafts before publishing.

## Verification and limits

Catalog coverage, placeholder/number preservation and fragment whitespace checks pass. Browser checks cover 320–1440 px, RTL/LTR switching, full-homepage routing, mobile Tools, Learn quiz feedback, Blog and Live weather. Windows build and embedded-resource/font/RTL-card checks pass. The complete native Windows app and paired-PC flows still need a human walkthrough. Companion personality sources, provider-returned content, and the bundled English PDF remain outside this catalog pass. The reviewed website Theory of Everything slides are translated.

The full local self-test completed with the same three environment failures seen in earlier localization passes: report-file access in the local Documents folder, access to recipient-key backup storage, and Ctrl+Alt+A already being registered by another app. Its embedded-language test passed. These failures have not been hidden or changed to passing tests.

The native-speaker proof check remains outstanding. The owner authorized the source push.
