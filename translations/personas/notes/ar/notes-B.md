# Notes B — Arabic persona translations (epic-fantasy, chill, hype, roast, bestie)

Translator: subagent session ee5ae35a (group B). Sources: the five English files in
`gpt-drop/you-will-be-taking-over-adeebometer/AdeeboMeter/knowledge/`, plus the
self-test rules in `src/Lines.cs` lines 210-240. comfort.txt was not touched.

## Voice choices

- **General (all five):** Modern Standard Arabic, pan-Arab, no dialect words.
  Tech terms kept as Arabic speakers actually say them: الواي فاي (Wi-Fi),
  الراوتر، السيرفر، بينق (ping)، DNS، VPN، MTU، Windows، Adeebo (in
  "جائزة Adeebo"). No emoji, no RTL/direction-override characters, plain text only.
  "{addr}" is addressed with "يا {addr}" (vocative) where the English uses the name.
  "bestie" is rendered "يا صديقي" (view.switch, quiz.wrong, market.open) — gender-neutral
  in the vocative; no gendered adjectives were used for the speaker anywhere
  (e.g. bestie sec.ok "Proud of you" became "أحسنت" rather than "فخور/فخورة بك").
- **Epic-fantasy:** high-fantasy quest register — المملكة، القلعة، الأسوار،
  الغربان، الكشافة، المجلس، الحكماء، الختم الملكي، النفق السري، الطريق الملكي.
  "maester" → الحكيم (no trademarked names). "kingsroad" → الطريق الملكي.
  Deliberately NOT carried over: the English "The night is dark and full of
  dropouts" (an echo of a Game of Thrones line) and "bent the knee"
  (a GoT phrase) — both reworded to plain fantasy Arabic.
- **Chill:** short, flat, unhurried MSA; "على مهل" for "easy does it";
  "سأنتظر" / "أراقب على أي حال" for the idle lines.
- **Hype:** exclamation energy kept with "!" on nearly every line; "هيّا"،
  "يا بطل"، "عدّاء السرعة" for "speedrunner/champ"; "بومة الليل" for "Night owl mode".
- **Roast:** the teasing is aimed only at the gear — الراوتر، الواي فاي،
  الجهاز، المزوّد، Windows. The user is never the butt of the joke
  (e.g. "Your PC is doing its best. Its best is slow" → "جهازك يبذل قصارى
  جهده. وجهده بطيء"). "Touch grass?" → "اخرج واستنشق الهواء؟" (the English
  idiom has no direct Arabic equivalent). "Ghosting" → "يتجاهلنا".
  "wet noodle" kept literally as "معكرونة مبللة" — it lands as an image in Arabic too.
- **Bestie:** warm and on the user's side — "لا تقلق"، "أنا هنا من أجلك"،
  "سنفعلها معًا"، "أنا معك"، "ليس خطأك". "Yay" → "رائع"; "Obsessed" →
  "مذهل" (no Arabic equivalent for the slang sense, kept the enthusiasm).

## Lines I was unsure about

1. **epic-fantasy / wifi.drops** — EN: "{v} times today the Wi-Fi bent the knee
   to the microwave." → AR: "{v} مرات اليوم استسلم الواي فاي أمام الميكروويف."
   "Bent the knee" is a Game of Thrones phrase, so I dropped the image and used
   plain "استسلم" (surrendered). Loses some of the joke; a fantasy-flavoured
   alternative would be "ركع الواي فاي أمام الميكروويف" but that keeps the
   same kneeling image the ban is meant to avoid.
2. **epic-fantasy / wifi.drops** — EN: "The signal fell {v} times. The night is
   dark and full of dropouts." → AR: "انقطعت الإشارة {v} مرات. الليل طويل
   والانقطاعات كثيرة." The second sentence is a GoT catchphrase echo, so I
   rewrote it rather than translating it. The fantasy mood survives; the
   reference does not.
3. **roast / stream.none** — EN: "Nothing streaming. Touch grass?" → AR:
   "لا شيء يُبث. اخرج واستنشق الهواء؟" "Touch grass" has no established Arabic
   equivalent; the literal "المس العشب" would read as nonsense. The chosen
   line keeps the teasing nudge ("go outside") without the idiom.
4. **roast / wifi.drops** — EN: "Signal strength of a wet noodle." → AR:
   "قوة إشارة كمعكرونة مبللة." Literal; it works as an image, but "wet noodle"
   is not an Arabic idiom, so it may read slightly odd. Alternative: "قوة
   إشارة كخيط مبلل" — same idea, no better.
5. **bestie / game.streak** — EN: "{v} in a row?! Obsessed." → AR: "{v} على
   التوالي؟! مذهل." "Obsessed" (slang: "I'm obsessed with this") has no
   natural Arabic equivalent; "مذهل" keeps the enthusiasm but drops the
   bestie-slang flavour.
6. **epic-fantasy / award** — EN: "The Adeebo Award. Kneel, and rise a legend."
   → AR: "جائزة Adeebo. اركع، وانهض أسطورة." "Kneel" here is generic fantasy
   (knighting), not a trademarked phrase, so it was kept.
7. **epic-fantasy / devices.found** — EN: "{v} souls dwell in your realm." →
   AR: "{v} أرواح تسكن مملكتك." "Souls" for devices is kept literally; it is
   the fantasy voice, not a dark topic (no death imagery intended).

## Verification (script, run after writing)

Per file: content line count = English count; keys in the same order with the
same variant counts; every {addr}/{v} preserved exactly; no '|' or tab inside
translated text; every line ≤ 16 words and ≤ 110 chars; no emoji; no
direction-override characters; no banned English words (Lines.cs regex).
A grep for Arabic equivalents of banned words (موت، قتل، دم، حرب، سلاح،
قنبلة، غبي، أحمق…) found only substring false positives ("قادم" contains "دم").

| file | content lines | max words | max chars |
|---|---|---|---|
| lines-epic-fantasy-ar.txt | 122 | 9 | 54 |
| lines-chill-ar.txt | 112 | 11 | 58 |
| lines-hype-ar.txt | 112 | 10 | 57 |
| lines-roast-ar.txt | 112 | 11 | 58 |
| lines-bestie-ar.txt | 105 | 12 | 61 |
| **total** | **563** | | |
