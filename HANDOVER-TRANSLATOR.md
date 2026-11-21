# AdeeboMeter - translator handover

For whoever translates AdeeboMeter. Written 17 Nov 2026 (app 1.1.5). The owner is Adeeb; he decides what ships.

## What can be translated today
| Part | Translatable now? | Where |
|---|---|---|
| **Windows app** - menus, settings, buttons, messages, tool names, most short results | **Yes** | `knowledge/lang-<code>.txt` |
| The buddy's personality lines (26 personas), Learn lessons, help texts, tool explanations | Not yet - English only | `knowledge/lines-*.txt`, `learn.txt`, `terms.txt` |
| Phone app and website (adeebometer.com) | Navigation and selected screens | `docs/locale-messages.js` |

The Windows app includes French (Canada), Spanish (`es`), Arabic (`ar`), Bengali (`bn`), Hindi (`hi`), and Simplified Chinese (`zh`) as previews.
The five reviewed tables have 246 entries each, including main navigation, Settings labels, and common Toolbox names.
The remaining strings fall back to English. Run the checker for the current coverage of each language.

## How it works (no coding needed)
- Each language is **one text file**: `knowledge/lang-fr.txt`. One line per text: **the English, a TAB, the translation.**
  ```
  Back to corner	Retour au coin
  Balanced	Équilibré
  ```
- The app looks up the **exact English** (same capitals, spaces, punctuation, `...` vs `…`). If the English doesn't match
  character for character, the line is ignored and the English shows - nothing breaks.
- Missing lines simply stay in English, so a half-finished file is safe to ship.
- Lines starting with `#` are notes. Save as **UTF-8**; use a real TAB (not spaces) between the two sides.
- The file is packed into the exe at build time - nothing is downloaded, so a change needs a new build to show.

## What's left to do
Run `python3 tools/langcheck.py fr` (or ask Adeeb to). It writes **`lang-fr-todo.txt`**: every English text the app
shows that isn't in `lang-fr.txt` yet, each followed by a TAB and nothing. Fill in the right-hand side and send it back
(or move the finished lines into `lang-fr.txt`). The script prints a summary like `fr: 1722 texts, 494 translated, 1228 to do`.
- It can't see texts the app builds while running (a number joined to words, e.g. ` Mb/s down (was `). Translate those
  pieces as they are - **keep leading/trailing spaces and punctuation exactly**, because the app glues them to numbers.
- Lines already in `lang-fr.txt` that the script doesn't list are reached another way (menu items, settings choices) -
  **keep them**.

## Rules
1. **Keep it short.** Buttons and labels sit in fixed-width spaces. Aim for the length of the English; at most about a
   third longer. If French needs much more room, use a shorter natural phrase rather than a literal one.
2. **Tone:** a friendly helper, plain words, never stiff or technical when the English isn't. Address the user with
   **vous** (Canadian French, Quebec-friendly vocabulary: "courriel", "magasiner" is too casual - "téléverser" is fine).
3. **Don't translate:** AdeeboMeter, Adeebo Points, protocol and standard names (DNS, DHCP, MTU, TCP, UDP, TLS, Wi-Fi,
   SNMP, BGP, IPv6, VPN), units (ms, Mb/s, GB -> use **Go/Mo** in French only where the English says GB/MB as a size),
   file extensions (.adeebo, .pcap), keyboard keys (Ctrl, Esc).
4. **Placeholders** like `{addr}` (how the user likes to be called) and `{v}` (a measured value) must stay exactly as they
   are, once each.
5. **Never add company or employer names** - the app's self-test rejects the language file if it finds any.
6. **Investing texts** are education only - no advice words ("buy", "sell now"); keep the meaning neutral.
7. Machine translation is fine as a first draft, but every line must be read by a person who speaks the language.

## Checking your work
- In the app: **Settings > General > Language > Français**, then restart AdeeboMeter.
- Look at: the sidebar, Settings (every section), Toolbox (by question and Every tool), Home's checkup, the corner widget,
  the tray menu, and one result of each kind (fine / problem). Note anything that's cut off ("…") or still English.
- Report cut-off text with a screenshot - it may need a shorter word, or a layout fix from the developer.

## Adding another language
Send the finished file as `lang-<code>.txt` (two-letter code: es, de, bn, ar...). The developer adds one line to
`src/Lang.cs` so it appears in Settings. Right-to-left languages (Arabic, Urdu) need layout work first - ask before
starting one.

## Arabic preview checks
Settings rows lay out choices from right to left in Arabic. The rest of the app still uses the original pane layout.
On Windows, check Arabic letter shaping, mixed labels with Wi-Fi and DNS, and button clipping at 100% and 125% display scale.
Also check that Bengali and Hindi vowels and conjuncts display correctly in the app fonts.

## Later (not yet)
When the buddy lines, Learn lessons and the phone app/website become translatable, they'll come with their own notes. The
buddy lines are personality, not literal text - they'll need a writer's touch, not a word-for-word translation.
