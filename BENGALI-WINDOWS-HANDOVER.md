# Bengali Windows app — 19 November 2026

This change completes the Windows translation catalog, separately from the website. Previous website progress did not mean the Windows app was translated.

- Previous Windows table: 304 entries. Updated table: 4,197 exact English/Bengali pairs.
- `tools/langcheck.py bn`: 2,037 literal UI texts, 2,037 translated, zero to do. The other 2,160 entries are reached through variables or content lookups.
- Broader Windows source inventory: 4,197/4,197 keys. Existing Bengali keys were checked and retained.
- Reused 1,536 existing translations or intentional technical identifiers. Added 2,661 editorial translations and corrected terminology. The owner must proofread the wording; generated coverage counts cannot establish native fluency.
- Validated placeholders, source numbers, real TAB separators, edge spaces and absence of draft markers. Tech names, units, commands and keys intentionally remain unchanged where appropriate.
- Bengali UI fonts now use Nirmala UI, with Vrinda/Segoe UI fallback. Startup and zoom paths use the selected language font. Existing Arabic behavior is retained.
- Verification passed: build, embedded 4,197-entry catalog, Bengali Settings lookup, font at 100% → 125% → 100%, native header/card rendering, and the existing Arabic regression script. Native text-preview PNG was visually inspected. Build retains the two existing compiler warnings in FullView.Labs.cs and Qr.cs.
- No website content changed in this task. The expanded shared web inventory currently finds 451 Bengali web keys missing; Windows coverage must not be presented as web coverage.

## Repeatable verification and proof export

```
python tools/langcheck.py bn
node tools/localization-audit.cjs bn --windows-only
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-bengali-build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-arabic-build.ps1
node tools/windows-proof.cjs bn ..
```

Run the build from the repository directory (the .NET packaging code uses the process working directory). Proof HTML is standalone and searchable; TXT contains every source/translation pair. Keep these separate from the actual site.

## Release and review boundaries

Local review build is unsigned/unofficial (no Hall of Fame release key). Owner review and publication remain pending. A source catalog cannot cover arbitrary runtime text, all buddy personalities, external service responses, or the separate English Theory of Everything PDF. Inspect Settings, Toolbox/results, Learn/diagrams, Games, Live data, tray and widget on the actual Windows app for clipping and natural phrasing before calling the whole product finished.

Previously observed full self-test environment failures concern Dropbox report-file access, P384 recipient-backup storage access, and Ctrl+Alt+A already used by the installed app. Do not describe those runs as wholly passing. Focused embedded-language/font/render checks are separate.
