# Spanish Windows translation — completed catalog draft, 19 November 2026

## Completion update

The initial-pass figures below are historical. The completed Windows table now has **4,197 English/Spanish pairs** at the time of writing (more were added later - run `python3 tools/langcheck.py es` for today's state), with **2,037/2,037 literal UI texts covered and zero to do**. Another 2,160 keys are reached through variables or content lookups. Shared Windows inventory coverage is 4,197/4,197. Placeholder, numeric-token, TAB-format and edge-whitespace checks pass. Original 246 keys are retained; 3,965 values were added or changed, zero removed.

All 3,839 initially missing keys have now been translated, plus corrections to reused theme/mode names and terminology (Equilibrado, Mercado, Zona de juegos). Includes settings, result fragments, help, Learn lessons and quizzes, diagram labels, labs, games, tray and widget text represented in the catalog. Native-speaker wording and actual full-screen layout review remain pending.

Website/mobile was completed in the subsequent web pass: see SPANISH-WEB-HANDOVER.md for its separate 3,738/3,738 count and browser checks. Buddy persona sources, external service responses and the separate English Theory of Everything PDF remain outside these catalogs. Do not report catalog counts as whole-product proof.

`tools/verify-spanish-build.ps1` checks every embedded source/translation pair, LTR direction, fallback, fonts at 100% → 125% → 100%, and renders a native header/card PNG. The local build remains an unsigned/unofficial 1.1.5 review build; no Spanish deployment occurred. Official release needs GitHub release build/provenance and the owner's Certum/SimplySign signing step. Existing compiler warnings remain FullView.Labs.cs CS1692 and Qr.cs CS0675.

The parent workspace retains frozen indexed batches for missing indices 0–3838. They are all applied; there are no remaining indices. To export the full change record, use pre-Spanish baseline `f52345b`. Full TXT and searchable HTML are generated from the actual Windows table with `tools/windows-proof.cjs es ..`.

## Reusable checks and exports

- python tools/langcheck.py es
- node tools/localization-audit.cjs es --windows-only
- powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-spanish-build.ps1
- node tools/windows-proof.cjs es ..
- node tools/windows-change-record.cjs es f52345b ../AdeeboMeter-Spanish-Windows-change-record.txt

The bilingual TXT lists all current pairs. The change TXT lists the English source, previous Spanish and current Spanish. The searchable HTML is a standalone proof document, not the live website or an app screenshot. Build from the repository directory. Native-speaker and full-screen layout review remain pending.
