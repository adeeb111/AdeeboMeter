# Notes — Hindi personas, group D (emo, coder, genz, dark-ages, industrial, mission)

Translator: subagent, 2026-11-17. Sources read-only; `comfort.txt` untouched.
Verification: all 6 files re-checked against `Lines.cs` Check() rules — 553/553 lines,
keys in identical order with identical variant counts, all `{v}`/`{addr}`/`{Addr}`
preserved, no `|`/tab inside text, max 14 words and max 60 chars per line,
no emoji, no banned-word regex hit. UTF-8, trailing newline, header copied verbatim.

## Shared choices
- Everyday spoken Hindi, mostly Devanagari script. Tech words kept as Hindi speakers
  actually say them: Wi-Fi, राउटर, पिंग, DNS, VPN, सर्वर, डाउनलोड, MTU, TLS, HTTP.
  AdeeboMeter, Windows, tool/acronym names never translated.
- No dark topics anywhere, even where the English leans that way (emo especially):
  no मृत्यु/खून/युद्ध or equivalents; "gone/lost" lines were softened to
  "बीत गया / चला गया / गायब हो जाते हैं" (vanish = गायब, not death).
- No insults aimed at the user in any persona, including genz ("cooked", "on read"
  kept as teasing, never stupid/idiot equivalents).
- Placeholders stay in Latin script exactly as in English (`{v}`, `{addr}`).

## Voice choices per persona

### emo — melodramatic, moody, self-aware
First-person, sighing Hindi: "मैं यहाँ हूँ", "समझ नहीं आ रहा", "हम सब ऐसे ही हैं".
Kept the persona's habit of turning a network fact into a feeling
("शांत है। शक होने वाली शांति।", "सर्वर ठीक है। तुम्हारा Wi-Fi किसी दौर से गुज़र रहा है।").
Deliberately light, never dark — the English "Like everything" / "It won't last"
lines are wistful, not grim.

### coder — programmer humour, Hinglish
Short, clipped, code-flavoured: "प्रोसेस शुरू", "बिल्ड ग्रीन", "नो एरर। नो वार्निंग।",
"grep करके देखो", "Assertion फेल", "Bisect points there" → "वहीं bisect करो".
English code words (build, commit, cache miss, timeout, 404, Status 200) kept in
Latin script because that is how Indian developers actually speak/write them.
**Trap found:** the English line "Hello, world" itself trips the app's banned-word
regex — `\bhell` prefix-matches "Hello". Translated as "हैलो, world" so the Hindi
file passes the self-test the English file would fail.

### genz — current Hinglish youth slang (DATES FAST — see below)
Heavy Roman-script slang mixed into Devanagari sentences, as actually spoken:
"so back", "lowkey elite", "Rare W", "cooked", "ghost कर दिया", "flaky", "fr",
"ate किया", "on it हूँ", "brb", "big L", "on read", "sus", "Locked in", "Main character energy".
Untranslatable catchphrases left in English rather than forced into Hindi:
"Ate that, no crumbs" and "Main character energy" are kept verbatim (with a Hindi
sentence around them where the English had one). **This file will date fastest:**
"ate / no crumbs / lowkey / fr / W / L / ghost / cooked / mid / vibe check" are
2024–2026 slang; expect a refresh pass within a year or two. "bestie" kept as-is.

### dark-ages — medieval voice, silly never grim
Archaic-register Hindi: "सुनो!", "हे", "दुर्ग", "राज्य", "प्रजा", "साधु", "लेखक",
"संदेशवाहक", "ढिंढोरची", "मोमबत्ती", "शुभ प्रभात", "मेरी सौगंध".
Modern tech words are the joke — they stay modern ("Wi-Fi", "DNS", "MTU", "फाइबर")
against the medieval frame, exactly as in English ("Witchcraft? No, good fibre").
"Thy" rendered as "तुम्हारा/तुम्हारे" (respectful, not the archaic "तेरा", which
would sound rude in modern Hindi). "Peasants" softened to "प्रजा" (subjects/people)
— "किसान" would be literal but loses the court joke; "प्रजा" keeps it.
"Bandits, surely" → "डाकू होंगे, ज़रूर" (no violence implied, just the joke).
"Enemy castle" → "दुश्मन के दुर्ग" — दुश्मन is mild, no war language used anywhere.

### industrial — factory / steam / machine voice
Workshop Hindi: भट्टी, गेज, पाइप, करघा, बेल्ट, वाल्व, तिजोरी, शिफ्ट, फोरमैन, सीटी.
"Works" → "कारखाना", "looms" → "करघे", "spanner" → "पेंच" ("पेंच फँसा दिया").
Kept the persona's concrete, physical metaphors: leaks, pressure, steam, crates/chutes.

### mission — mission control, calm NASA-style procedure
Flat, procedural Hindi: "मिशन कंट्रोल ऑनलाइन है", "सभी सिस्टम go हैं",
"टेलीमेट्री सामान्य है", "स्टैंडबाय", "कन्फर्म", "नेगेटिव", "no-go".
"Flight" (the addressee) kept transliterated as "फ्लाइट" — there is no natural
Hindi equivalent for the NASA call-sign, and translating it as "उड़ान" would read
wrong. "Go / no-go" also kept in Latin script, as in real Indian mission-control
/ aviation usage. "Downrange" → "आगे की ओर" (no exact Hindi term; deliberately plain).

## Lines I was unsure about

| key | English | My Hindi | Why unsure |
|---|---|---|---|
| emo / market.open | The markets opened. The void opened too. | बाज़ार खुल गए। खालीपन भी खुल गया। | "The void" is emo-absurd; खालीपन (emptiness) is the closest natural Hindi, but loses the joke's punch. Alternative: "सूनापन भी खुल गया" — no better. |
| emo / game.timeup | Time's gone. Like everything. | समय बीत गया। सब कुछ की तरह। | Softened on purpose (no dark wording allowed); "सब कुछ की तरह" is vague in Hindi. Acceptable, but the wistfulness is thinner than English. |
| emo / speedrunner | Still finishing. Patience is a vibe. | अभी खत्म कर रहा हूँ। सब्र भी एक vibe है। | Kept "vibe" in Latin script — natural in Hinglish, but a purer-Hindi reader may prefer "सब्र भी एक अंदाज़ है". I kept vibe for the emo voice. |
| emo / storm.near | A storm is coming. Fitting. | तूफ़ान आ रहा है। माहौल के हिसाब से है। | "Fitting" (i.e. fitting my mood) is hard in one short line; "माहौल के हिसाब से है" is the closest I got. |
| genz / quiz.right | Ate that, no crumbs. | Ate that, no crumbs। | Left entirely in English. Any Hindi rendering ("खा गए, कोई कसर नहीं") kills the slang. Will date fast; flag for review when slang moves on. |
| genz / award | You got the Adeebo Award. Main character energy. | तुम्हें Adeebo Award मिला। Main character energy। | Same: "Main character energy" has no Hindi equivalent that lands. Kept verbatim deliberately. |
| genz / inet.slow | Home network ate. The internet past it did not. | होम नेटवर्क ने ate किया। आगे का इंटरनेट नहीं। | "Ate" as slang ("did great") is untranslatable; "ने ate किया" is how Hinglish speakers actually say it, but it may confuse a non-slang reader. |
| dark-ages / quiz.wrong | Wrong, by my troth. Try again. | गलत है, मेरी सौगंध। फिर कोशिश करो। | "By my troth" (medieval oath) → "मेरी सौगंध" is formal/archaic but more solemn than the English joke. "मेरी कसम" would be more everyday; सौगंध chosen for the old-time register. |
| dark-ages / speed.great | Download {v}. Witchcraft? No, good fibre. | डाउनलोड {v}। जादू? नहीं, अच्छा फाइबर है। | "Witchcraft" → "जादू" works, but the English joke lands on "good fibre" as the punchline; in Hindi the punchline is flatter. No better short option found. |
| dark-ages / all.good | The peasants report no problems. PC, Wi-Fi, internet fine. | प्रजा कहती है कोई दिक्कत नहीं। PC, Wi-Fi, इंटरनेट ठीक हैं। | "Peasants" → "प्रजा" softens the class joke; literal "किसान" would be odd (farmers reporting on Wi-Fi). Judgment call. |
| mission / app.open | Flight, we're go for network monitoring. | फ्लाइट, नेटवर्क मॉनिटरिंग के लिए हम go हैं। | "Flight" as a call-sign has no Hindi form; transliteration is the only option, but a Hindi-only reader won't know why they're being called फ्लाइट. |
| mission / mtr.lossy (2nd) | Loss begins at hop {v}. Downrange. | लॉस हॉप {v} से शुरू होता है। आगे की ओर। | "Downrange" has no Hindi equivalent; "आगे की ओर" is plain filler. Could also be dropped, but variant count must stay exact, so a short tail was needed. |
| coder / app.open | Hello, world. Monitoring {addr}'s network. | हैलो, world। {addr} का नेटवर्क मॉनिटर हो रहा है। | See trap above: Latin "Hello" fails the banned regex (`hell`). हैलो in Devanagari passes. Worth flagging to the parent: the English source line itself would fail Check(). |
| industrial / update.suspect | The {v} update threw a spanner in. | {v} के अपडेट ने पेंच फँसा दिया। | "Threw a spanner in (the works)" → "पेंच फँसा दिया" is idiomatic enough, but "spanner" itself (रिंच/पाना) is lost. Fine for the voice. |

## Not translated
- `comfort.txt` — left entirely alone, per task.
- Nothing in the GPT drop or any repo was modified (read-only).
- Other files already present in `corrected/personas/hi/` (bro, chill, nature, poetic,
  revolution, epic-fantasy, scientist, shakespeare, space-saga, hype) are other
  agents' work; not touched, not verified here.
