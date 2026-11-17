# AdeeboMeter - UI/UX handover

For the UI/UX designer joining AdeeboMeter. Written 17 Nov 2026 (app 1.1.5 is live; 1.1.6 is being planned on
`dev-1.1.6`). The owner is Adeeb: **show designs first and wait for his go** - nothing is built from a design he hasn't
approved, and everything ships together in one release.

## The product in one breath
A free Windows app that tells you in one sentence whether your PC, Wi-Fi, router, provider, DNS or the internet is the
problem - and turns it into a picture you can send to anyone. Motto: **"Know what's wrong. Show anyone."**
Most people open it **when something is already broken**, often not technical, often annoyed. Design for that moment.

Three surfaces, one look:
| Surface | What it is | Built with |
|---|---|---|
| **Windows app** | Corner widget (tiny, faded) + full window (sidebar, pages, 158 tools) + tray menu | C# WinForms, every pixel owner-drawn (no web view) |
| **Phone app** | adeebometer.com/app - works alone (phone checks, Learn, Games, Live) and as a remote for the PC | Plain HTML/CSS/JS, installable web app |
| **Website** | adeebometer.com - home, mode pages, Learn (91 lessons), Games, Live, Files | Plain HTML/CSS, GitHub Pages |

## Adeeb's design rules (standing)
1. **Short text.** One or two lines, plain words. People don't read long text - especially when something's broken.
2. **A companion, not a robot.** A friendly buddy line (moods and personas the user picks), never cringe, never bigger
   than the user, never needy. Facts always come first; the joke never hides the answer.
3. **No pop-ups, no alerts, no alarms, no fake urgency, no streaks.** Questions (like "Allow this phone?") are answered
   on the page, in place.
4. **Flat and calm.** Flat UI, easy on the eyes. Colour stays consistent with the AdeeboMeter theme - no swinging between
   extremes; section colours only to tell sections apart.
5. **Icons are 2D vector, never emoji** (emoji render differently everywhere; some symbols show as boxes on some
   systems - draw them).
6. **The logo is fixed:** blue rounded-square gauge (green/yellow/red arcs, white needle) over a three-dot path line;
   wordmark **Adeebo**Meter with "Meter" in blue. Same everywhere.
7. **Never look hung.** Whenever it's busy, something on screen says so (a mood-based "working on it" line, progress).
8. **Motion is small and purposeful** (Apple-like details): the logo needle sweeps when the window opens and breathes every
   5 minutes, the buddy "types" its line (2 s at most), scrolling glides. **All of it off when Windows' "Show animations"
   is off** (and `prefers-reduced-motion` on the web).
9. **Penny app:** light on the PC. No heavy effects, no video backgrounds, no constant redraws.
10. **Consistent navigation** across PC, phone and web; the phone should feel smooth and never throw the user out of the app.
11. **No employer/company names, no tracking, no GitHub mentions** outside About.

## Where things are
| What | Where |
|---|---|
| PC colours, fonts, sizes | `src/Ui/Theme.cs` (light and dark; `S` = the screen's scale, everything is sized `n * S`) |
| PC fonts | Inter (writing), Nunito Bold (small writing) - packed in the app |
| PC pages | `src/Ui/FullView.*.cs` (one file per page: Home, Toolbox, Index = Every tool, Phone, Settings...) |
| Phone/web colours | `docs/ui.css` - tokens (`--page --card --line --ink --sub --blue --green --amber --red`) plus palettes (default navy, neon, midnight, sunset, mint, light) |
| Phone/web icons | `docs/ui.js` (one SVG set, used by every page) |
| Earlier mockups | `tools/design/*.html` (app v2-v5, web v3 flat, live v2, home v2) - open in a browser |
| Design board | A claude.ai "Designs" board with the latest boards (PhoneHub, Link icon, Motion, All tools) - ask Adeeb for the link |

## How to see every screen
- Install 1.1.5 from adeebometer.com (or the Microsoft Store) on Windows; try **light and dark**, **100 % and 150-200 %
  display scaling**, a **narrow window** and a **wide one**.
- View presets (top bar, "View") change what's shown: try My setup, Gamer, Trader, Streamer.
- Phone: open adeebometer.com/app on an iPhone and an Android phone; link it to the PC from the PC's **Your phone** page.
- The developer can screenshot any page directly (`--view <page>`), so ask for a set instead of clicking through.

## Known problems and open asks (start here)
1. **Phone app and website need a major UI pass** - Adeeb's words. Special attention to the **phone widget** (a phone
   version of the PC's corner widget) and **iPhone** users. A **tablet** layout is wanted too.
2. **Toolbox / Every tool** - 158 tools: finding the right one must stay easy as more arrive. The current answer is "by
   question" + an index with pinned favourites; a scroll blink is fixed and scrolling glides from 1.1.6. Review it with
   fresh eyes.
3. **Settings** were hard to find things in (now question cards, General first) - check whether that works for a newcomer.
4. **Consistency** between PC, phone and web: same names for the same things, same icons, same order.
5. **The website's new top** went live 17 Nov (navy by default, the headline "Find out why your internet or PC is slow.
   And much more.", motto pill, example result, phone + signed badge). The rest of the page follows the same cards -
   review the whole scroll, phone first.
6. **Accessibility**: the PC app reports its painted buttons to screen readers and supports keyboard focus; check contrast
   on every palette (the phone's neon/sunset/mint ones especially) and focus rings on the web.

## How to deliver
- **Mockups first** (Figma or HTML - either is fine), at desktop 1440 px, phone 390 px, and the PC window at 1280x860.
- Mark what changes and why, in one line each. Adeeb approves; the developer builds; screenshots come back for a final look.
- Keep within the rules above - especially short text, no pop-ups, flat, the fixed logo, and motion that can be switched off.
- Copy changes are welcome, but every claim must be true of the app today.
