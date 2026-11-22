# Persona translation notes — Arabic (ar)

Merged from the per-group notes written by the translator agents. 2,819 lines across 29 persona files.
These are draft translations a native speaker should sanity-check before shipping.

---

## Group A

# Notes — Arabic persona translations, group A (bro, neteng, plain, aristotle, confucius, tao)

Translator notes for the six files in this group. All 576 lines translated; verification
(counts, key order, variant counts, placeholders, ≤16 words, ≤110 chars, no emoji,
no RTL controls, no banned words) passes — see the verification summary at the end.

## Voice choices

- **General register:** Modern Standard Arabic, pan-Arab. No dialect-specific words
  (no Egyptian/Levantine/Gulf-only forms). Casual lines use MSA-friendly colloquial
  touches that read naturally everywhere: هلا، تمام، اهدأ، يا صاحبي.
- **Bro (يا صاحبي):** every line addresses the user as يا صاحبي, the Arabic "bro".
  Kept the matey, reassuring tone; slang is recreated, not translated word-for-word.
  "goated" → أسطوري; "Beast mode" → وضع الوحش; "speedrunner" → يا سريع.
  "Clean as a fresh fade, bro" → نظيفة كالجديد تماما يا صاحبي (the haircut joke
  does not travel; kept the "like-new clean" sense instead).
  "fake news" → مزيف (dropped the political catchphrase; the meaning is "fake").
- **Neteng:** dry, telegraphic, jargon kept in Latin where engineers actually write
  it that way (RTT, DNS, TCP, MTR, ARP, ICMP, SYN-ACK, PMTUD, SQM, QoS, RTP, CDN,
  AES-256, MAC, TTL, ACK/NACK, L1/L2/L1-L7, WAN, CPE, DF). Arabic glosses the
  concepts (قفزة = hop, فقدان = loss, اهتزاز = jitter, تذبذب = flap, مجس = probe,
  عنق الزجاجة = bottleneck). "flapped x{v}" kept the literal "x{v}" form.
  port.open "TCP {v} open, handshake in {v}" keeps BOTH {v} placeholders, as in English.
- **Plain:** short factual MSA, no persona colour, the fact always survives.
  "bufferbloat" kept in Latin (no established Arabic term; same as neteng).
- **Aristotle:** aphoristic, cause-and-effect, "as it ought" / mean / habit language:
  كما ينبغي، الوسط، العادة تصنع الخبير، التميز عادة. "the agora" → الأغورا
  (transliterated, not flattened to السوق, to keep the Greek colour).
  "knowledge rightly held" → معرفة ممسوكة بحق.
- **Confucius:** order, diligence, propriety, correction: في موضعه الصحيح، اجتهاد،
  صحّح، الصبر مناسب هنا. "the prudent" → العاقل (vs Aristotle's الحكيم for
  "the wise man", to keep the two personas distinct).
- **Tao:** water / flow / stillness imagery throughout: يتدفق، التيار، النهر،
  الماء الساكن، سكون، انحنِ كالقصب. "gathered without striving" → جُمعت بلا سعي.
  "Time has passed, as it does" → مر الوقت، كما يمر دائما.

## Shared term choices

- Wi-Fi → واي فاي; router → راوتر; server → سيرفر; ping → بينق; download (noun) →
  تحميل; VPN, DNS, Windows, AdeeboMeter, Adeebo Award → kept in Latin, never translated.
- "provider" → مزود الخدمة. "broker" → الوسيط. "hop" → قفزة. "gateway" → البوابة.
- {addr} appears only in the three wisdom personas' app.open lines; preserved exactly.
- No emoji, no '|' or tabs inside text, no RTL/direction-override characters.
  Arabic diacritics used sparingly (مشفّر، فُتح، مُرشَّح) only where they disambiguate
  pronunciation; they count as characters but every line stays ≤65 chars.

## Lines I was unsure about

1. **bro / all.good — "Clean as a fresh fade, bro."**
   EN: "Clean as a fresh fade, bro." → AR: نظيفة كالجديد تماما يا صاحبي.
   Why unsure: "fresh fade" is a barbershop reference (a fresh haircut). There is no
   pan-Arab equivalent that lands the same way, so I kept the "spotlessly like-new"
   sense and dropped the haircut image.
2. **bro / speed.great — "Bro, your internet is goated."**
   EN: "Bro, your internet is goated." → AR: يا صاحبي، إنترنتك أسطوري.
   Why unsure: "goated" (GOAT = greatest of all time) is youth slang; أسطوري
   ("legendary") is the closest MSA-friendly equivalent but loses the acronym joke.
3. **aristotle / market.open — "The agora opens for trade."**
   EN: "The agora opens for trade." → AR: الأغورا تفتح أبوابها للتجارة.
   Why unsure: transliterating الأغورا keeps the Greek flavour but some readers
   won't know the word; the alternative (السوق يفتح للتجارة) is clearer but loses
   the persona colour. I kept the transliteration.
4. **neteng / inet.slow — "Latency added upstream of the gateway."**
   EN: "Latency added upstream of the gateway." → AR: تأخير مضاف قبل البوابة من جهة المزود.
   Why unsure: "upstream" has no single-word Arabic equivalent in this context;
   I rendered it as "before the gateway, provider-side". The fact (delay is beyond
   the gateway) survives, but the wording is a small paraphrase.
5. **bro / encrypt.done — "Encrypted. Only your people can open it."**
   EN: "Encrypted. Only your people can open it." → AR: مشفّر. فقط من تثق بهم يفتحه.
   Why unsure: "your people" (your trusted circle) has no compact MSA equivalent;
   "those you trust" is the closest faithful rendering.
6. **tao / storm.near — "The storm comes; bend like the reed."**
   EN: "The storm comes; bend like the reed." → AR: العاصفة تأتي؛ انحنِ كالقصب.
   Why unsure: the reed-bending image is a classic Taoist image; القصب (reed/cane)
   is correct, but some readers may picture sugarcane rather than a river reed.
   The imperative انحنِ keeps the aphorism form.
7. **bro / pc.busy — "Too many tabs, bro. Your PC is tapping out."**
   EN: "Too many tabs, bro. Your PC is tapping out." → AR: تبويبات كثيرة يا صاحبي. حاسوبك يستسلم.
   Why unsure: "tapping out" (MMA submission) → يستسلم ("giving up") keeps the
   sense but loses the fight metaphor; acceptable, but flatter than the English.

## Verification summary (scripted, against the English sources + Lines.cs rules)

- bro: 136/136 lines; neteng: 136/136; plain: 136/136; aristotle: 56/56;
  confucius: 56/56; tao: 56/56. Total 576.
- Key order and per-key variant counts identical to the English files in all six.
- Header comment lines copied verbatim (including the mid-file batch-2 comment
  in bro/neteng/plain).
- Every {v} / {addr} / {Addr} preserved with the same count per line
  (including neteng port.open, which has {v} twice in both languages).
- No '|' or tab inside any translated text; UTF-8; trailing newline on all files.
- Longest line: 12 words (aristotle/tao), 65 characters (plain/aristotle) —
  well inside the 16-word / 110-char limits.
- No emoji, no surrogate characters, no RTL/direction-override characters.
- No English banned words (Lines.cs regex) and no whole-word Arabic equivalents
  of the banned list (موت، قتل، انتحار، دم، حرب، غبي...). Note: a naive substring
  check flags دم inside الخدمة / يستخدم / يخدم — those are not the word دم
  (blood); a whole-word check returns zero hits.
- comfort.txt was not read for translation and was left untouched.

---

## Group B

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

---

## Group C

# Notes C — Arabic persona translations (noir, pirate, gamer, support, bossy, stoic)

Translator group C. 581 content lines total. All files verified against the
self-test rules in src/Lines.cs (lines 210-240): counts match English exactly,
keys in the same order with the same variant counts, placeholders preserved
exactly ({v} / {addr} only — no {Addr} occurs in these six files), no `|` or
tab inside translated text, every line ≤ 16 words (max observed: 11) and
≤ 110 characters (max observed: 66), no emoji, no RTL/direction-override
characters, plain UTF-8 with trailing newline. Header comment lines and the
1.1.5 batch-2 comment lines copied verbatim (source files are CRLF; outputs
are LF, text identical).

## Shared conventions

- MSA throughout, no dialect words. No diacritics (tashkeel) anywhere, to
  keep text plain and short.
- Tech terms kept as Arabic speakers say them: واي فاي (Wi-Fi), الراوتر,
  السيرفر, البينق (ping), اللاغ (lag), DNS, VPN, MTU, NAT, Windows,
  التنزيل (download). AdeeboMeter, Windows, tool/protocol names (DNS, VPN,
  MTU, NAT, Alt+F4) and acronyms never translated.
- Addressee: masculine singular imperative / second person throughout, as
  the English uses "you" generically. {addr} is the user's chosen name and
  is kept in place.
- No banned content: no death/killing/war/blood words in Arabic either, no
  insults aimed at the user, no profanity, no trademarked names or famous
  catchphrases. Where the English leaned on a dark or violent image, the
  Arabic paraphrases it (e.g. noir "The trail's gone cold" → الأثر بارد).

## Voice choices per persona

### Noir (lines-noir-ar.txt, 105 lines)
Hard-boiled detective in MSA: short declarative sentences, "القضية",
"الملف", "الأثر", "المشتبه به", "بريء", "القضية مغلقة". The user is
"يا صاحبي" (for English "pal") and "يا محقق" in points.earned. "The city"
stays المدينة throughout; "the streets" الشوارع. "Smooth operator" became
أداء سلس (the personified wordplay does not survive — see unsure list).
"Case file open" → الملف مفتوح. "Every packet's a witness" → كل حزمة شاهد.

### Pirate (lines-pirate-ar.txt, 105 lines)
Swaggering sailor, vocative يا بحار / يا قبطان, sea vocabulary throughout:
السفينة، الأشرعة، الميناء، البحر، الكنز، الغنيمة، الحبال، العنبر.
"Doubloons" transliterated as دوبلون / دوبلونات (see unsure list).
"Crow's nest" → برج المراقبة (see unsure list). "Avast!" → توقف!
"False flag" → راية مزيفة. "Parley" → مفاوضات. "Shipwrecked" →
تحطمت السفينة. Kept the two-sentence sea/weather rhythm of the English.

### Gamer (lines-gamer-ar.txt, 105 lines)
Gaming slang transliterated where Arabic gamers actually use it: جي جي
(GG), اللاغ (lag), البينق (ping), الرانكد (ranked), إكس بي (XP),
الردهة (lobby). "Respawn" paraphrased as عدت للحياة / عد للحياة;
"speedrunner" as لاعب سرعة; "AFK" as وضع الغياب; "boss fight" as
معركة الزعيم; "carrying" as تحمل الفريق / احمل الفريق. Alt+F4 kept in
Latin. "Bufferbloat" rendered explanatorily as تضخم المخزن (see unsure).
"Rage quit" → انسحاب غاضب (see unsure). "Flawless route" → مسار مثالي.

### Support (lines-support-ar.txt, 112 lines)
Warm, calm, reassuring MSA: أنا هنا، لا تقلق (implied, never the blunt
"لا تقلق" alone), سنفهم الأمر معا، ليس خطأك، سأكون هنا حين تعود.
Reassurance is always paired with a concrete next step (restart, move
closer, try the cable), mirroring the English. "Lovely." → جميل.
"Well done" → أحسنت. "Proud of the work" → فخور بعملك.

### Bossy (lines-bossy-ar.txt, 98 lines)
Short imperatives, orders aimed at the gear, not the user: أغلق، أصلح،
انتظر، افحص، اتصل بمزودك. Punchy fragments: صحيح. التالي. / مغلق. تابع.
"As it should be / as ordered / as required" → كما يجب / كما أمرت /
كما هو مطلوب. "Home network passes. Provider fails." kept as a
school-grade metaphor: شبكة البيت ناجحة. والمزود راسب. (see unsure).
"Mtr.clean — Dismissed." → انصرف. (see unsure).

### Stoic (lines-stoic-ar.txt, 56 lines)
Marcus Aurelius in spirit, own lines, no quotations: calm aphoristic MSA
built on ما في وسعنا / ليس عبئا تحمله / ولكل شيء موسمه / ولكل خيار
ثمنه / العجلة ليست حكمة / ابق متواضعا / افعل ولا تتفاعل. "The present
moment is enough" → واللحظة الحاضرة كافية. "Rest is also a duty" →
والراحة واجب أيضا. Kept the English one-line-per-thought shape; longest
lines here (11 words) are the two that carry a full aphorism plus the
practical instruction (wifi.drops, net.none).

## Lines I was unsure about

| key | English | My translation | Why unsure |
|---|---|---|---|
| noir / stream.on | {v} plays clean. Too clean. I am watching. | {v} يعمل بسلاسة. نظيف أكثر من اللازم. أراقب. | "Too clean" (suspicious) is softened; "نظيف بشكل مريب" would be more noir but heavier. |
| noir / speed.great | Download {v}. Smooth operator. | التنزيل {v}. أداء سلس. | The personified "operator" wordplay is lost; no Arabic equivalent that fits the limit. |
| noir / game.timeup | Time ran out. It always does. | انتهى الوقت. وهو ينتهي دائما. | Fatalism kept, but flatter than the English; "هكذا الوقت دائما" is an alternative. |
| noir / quiz.right | Right on the money. | إصابة في الصميم. | Idiom replaced with an archery image; "أصبت الهدف تماما" is plainer and also fine. |
| pirate / app.open | Ahoy, {addr}! Lookout's in the crow's nest. | أهلا يا {addr}! الحارس في برج المراقبة. | "Crow's nest" has no standard Arabic nautical equivalent; literal عش الغراب would confuse. |
| pirate / points.earned | +{v} doubloons. | +{v} دوبلونات. | Transliteration: pirate flavour kept, but many readers will not know دوبلون; "قطع ذهبية" is clearer but blander. |
| pirate / speedrunner | Avast! Still on the last one. | توقف! ما زلت في المهمة السابقة. | "Avast!" has no MSA nautical interjection; توقف loses the sea voice. |
| pirate / busy | Swabbing the packets. | أنظف الحزم. | The deck-swabbing joke is mostly lost; "أمسح سطح الحزم" would be stranger. |
| gamer / wifi.drops | Wi-Fi disconnected {v} times. That's a rage quit. | الواي فاي انقطع {v} مرات. هذا انسحاب غاضب. | Literal; loses the slang punch. Transliterating as ريج كويت would be gamer-authentic but obscure to non-gamers. |
| gamer / back.again | Respawned. {v} things happened. | عدت للحياة. حدثت {v} أمور. | No standard Arabic word for "respawn"; "ريسبون" is used by some gamers but not all. Same for trade.closed "عد للحياة عند 9:30". |
| gamer / speedrunner | Speedrunner detected. Still on the last one. | تم رصد لاعب سرعة. ما زلت في السابقة. | "لاعب سرعة" is a paraphrase; "سبيدرنر" transliteration was considered and rejected as too obscure. |
| gamer / speed.bloat | Bufferbloat: {v} extra under load. That's your lag spike. | تضخم المخزن: {v} إضافية تحت الحمل. هذه قفزة اللاغ. | "تضخم المخزن" explains but loses the term; keeping Latin "bufferbloat" was considered. |
| gamer / search.empty | Type it: "lag", "ping", "wifi". | اكتب: لاغ، بينق، واي فاي. | Examples transliterated rather than quoted in English; assumes the user reads the transliterations. |
| support / wifi.drops | Your Wi-Fi has been unsteady. I saved what happened before each drop. | واي فاي غير مستقر. وحفظت ما حدث قبل كل انقطاع. | "حفظت" implies the app stored diagnostics; kept because the English says so, but a native reviewer should confirm the app really does this. |
| bossy / inet.slow | Home network passes. Provider fails. | شبكة البيت ناجحة. والمزود راسب. | School-grade metaphor (pass/fail) preserved; slightly odd in Arabic but funny-strict, which fits bossy. |
| bossy / mtr.clean | Clean path. Dismissed. | مسار نظيف. انصرف. | "انصرف" could read as ordering the user to leave; alternative: "أغلق الملف." |
| bossy / devices.found | Found {v}. Label the strangers. | وجدت {v}. حدد الغرباء. | "الغرباء" for unknown devices is fine, but "حدد" is softer than the English "label"; "وسم الغرباء" is more literal but less natural. |
| stoic / game.timeup | Time is up. It was never ours to keep. | انتهى الوقت. ولم يكن لنا أبدا. | Compressed; "الوقت لم يكن ملكنا يوما" is clearer and more aphoristic — consider swapping. |
| stoic / wifi.drops | The Wi-Fi left {v} times. Move closer; the rest is not ours to fret over. | الواي فاي غادر {v} مرات. اقترب، والباقي ليس مما نقلق بشأنه. | Longest stoic line (11 words); the second clause is a mouthful. "والباقي خارج وسعنا" is shorter and more Stoic. |
| stoic / net.none | No network. Annoyance changes nothing. Check the cable or the Wi-Fi. | لا شبكة. والانزعاج لا يغير شيئا. افحص الكابل أو الواي فاي. | Good, but "الانزعاج" is slightly formal; "الغضب لا يغير شيئا" is punchier. |

## Things a native-speaker reviewer should check

1. Pirate دوبلون vs قطع ذهبية (see above) — flavour vs clarity.
2. Gamer transliterations (جي جي، الرانكد، إكس بي، اللاغ) — all are what
   Arabic-speaking gamers actually write, but a reviewer from a different
   gaming community may prefer different spellings (e.g. رانكد vs رانك).
3. Bossy mtr.clean انصرف — confirm it reads as dismissing the case, not
   the user.
4. Masculine singular throughout: the English "you" is generic; if the
   app ever addresses a feminine user, these imperatives (أغلق، انتظر،
   افحص…) would need a feminine set — out of scope for this task, but
   worth flagging.
5. "الراوتر" vs "الموجّه": I used الراوتر everywhere (per the brief);
   lang-ar.txt uses الموجّه — the two should be reconciled for
   consistency across the app.

---

## Group D

# Arabic persona translations — notes (group D: emo, coder, genz, dark-ages, industrial, mission)

553 content lines total: emo 98, coder 91, genz 91, dark-ages 91, industrial 91, mission 91.
All six files verified against the Lines.cs self-test rules: same line count, key order and
variant counts as the English files; headers copied verbatim; every {v}/{addr}/{Addr}
preserved exactly; no `|` or tab inside text; every line ≤ 16 words and ≤ 110 characters;
no emoji; no RTL/direction-override characters; no banned English words (and no Arabic
equivalents of the banned dark topics, slurs or profanity). Plain Arabic text only.

## Voice choices

- **General / MSA base:** All six personas are in Modern Standard Arabic, pan-Arab, no
  dialect-specific words. Tech terms kept as Arabic speakers actually say them:
  واي فاي / Wi-Fi, راوتر, سيرفر, بينق, DNS, VPN, MTU, TLS, HTTP, Bufferbloat. AdeeboMeter,
  Windows, tool/acronym names and file names never translated. {addr} is left as the
  placeholder (the app fills a name/address); Arabic vocative/address forms around it
  (e.g. «أهلًا {addr}»، «أيها {addr} الطيب») keep it grammatical in either case.
- **Emo (melodramatic, moody):** soft, sighing register — «شعور غريب، بصراحة»،
  «هادئ بشكل مريب»، «ألسنا جميعًا كذلك». Deliberately kept light/relatable, never dark:
  «مثل كل شيء» for "Like everything" / "Time's gone. Like everything" rather than any
  death imagery; «الفراغ» for "the void" in market.open (existential sigh, not a dark topic).
- **Coder (programmer humour):** literal programmer vocabulary kept in Arabic where it
  reads naturally — حلقة، استثناءات، جمع القمامة، بناء أخضر، تأكيد، بصمة، مصافحة TLS —
  with English terms left as-is where Arabic speakers use them (404, Cache → «ذاكرة مؤقتة»,
  Bufferbloat, grep example kept as `"slow"`, `"dns"`, `"wifi"`). «أهلًا بالعالم» for
  "Hello, world" — the classic first-program greeting, not a generic hello.
- **Gen-Z (pan-Arab youth slang — DATES FAST):** this is the voice most likely to age.
  Used only slang that is already broad and pan-Arab rather than dialect-only:
  فخم، جنونية، كلاسيكي، دراما، طاقة البطل، أيقونية، على مهلك، «أنا عليها»، «أتقنتها تمامًا»
  for "Ate that, no crumbs" (the literal food idiom has no Arabic equivalent).
  Deliberately avoided: «cooked» rendered as منهك (not a slang calque), «ghosted» as
  تجاهلك, «left you on read» as تركك بلا رد, «mid» as متوسط, «lowkey» dropped or rendered
  as بهدوء/بصراحة, «fr» as فعلًا, «W» as فوز, «L» as خسارة كبيرة لهم. «Main character
  energy» → طاقة البطل. Re-check this file in a year; the slang will be the first thing
  to feel stale, and a refresh should re-audit only these lines, not the MSA base.
- **Dark Ages (medieval voice):** archaic-but-readable MSA: اسمع!، صبرًا!، أيها، هلمّ-style
  address, قلعة/مملكة/منادي/كاتب/رهبان/مخطوطات/حمام الزاجل/الجسر المتحرك/الختم الملكي.
  Kept it silly, never grim: «كالعنزة» for the wandering Wi-Fi, «دجاجة وحيدة»,
  «لصوص، بلا شك» for packet loss. «Good morrow» → صباح الخير (no archaic Arabic
  equivalent that reads naturally); «by my troth» → أقسم. «Thou art knighted» →
  نُلت الفروسية. Avoided any grim/death wording even where the English is only mildly
  dramatic («احترقت الشمعة» for "The candle has burned down" — a spent candle, not a
  dark image).
- **Industrial (factory/machine voice):** steam/factory lexicon throughout: غلايات،
  وردية، مقاييس، تروس، أنابيب، صمام، أنوال، تلغراف، صندوق حديدي، وسام المشرف،
  «ألقى مفتاحًا في التروس» for "threw a spanner in". «The works hum along nicely» →
  الأعمال تهمهم بلطف (kept the hum, not a literal translation).
- **Mission (mission control, calm NASA-style procedure):** clipped procedural register:
  «نوصي بـ»، «إرشاد:»، «بلا شذوذ»، «حالة ثابتة»، «قف بالانتظار»، «يا طيران» for "flight"
  (the NASA call-sign address, kept as a vocative), «الساعة عند الصفر» for "Clock's at
  zero". «You are go» / «{v} is go» → أنت جاهز / {v} جاهز (no Arabic "go/no-go" idiom;
  «غير جاهز» for no-go). «Downrange» → بعيدًا (kept short; «في المدى البعيد» would also
  work but costs words).

## Lines I was unsure about

1. **genz — quiz.right | "Ate that, no crumbs." → «أتقنتها تمامًا.»**
   The English is a slang idiom ("ate" = nailed it, "no crumbs" = flawlessly). No Arabic
   youth equivalent carries both the eating metaphor and the meaning, so I rendered the
   meaning (nailed it perfectly) and dropped the metaphor. Alternative: «أكلتها، بلا فتات»
   would be a confusing literal calque — rejected.
2. **genz — quiz.wrong | "Nah that's not it, bestie." → «لا، ليس هذا يا صديقي.»**
   «bestie» has no pan-Arab youth equivalent that isn't dialect-bound (يا صاحبي / يا قلبي
   are regional). «يا صديقي» is safe MSA but loses the Gen-Z warmth; acceptable trade-off
   for pan-Arab reach. Flag for a native-speaker check.
3. **genz — speedrunner | "Speedrun any%? Still on the last one." → «سباق سرعة؟ ما زلت في السابقة.»**
   "any%" is a speedrunning term with no Arabic equivalent; I dropped it and kept
   «سباق سرعة» so the gaming reference survives. Loses the in-joke, keeps the meaning.
4. **coder — dns.slow | "Lookups at {v}. Cache miss city." → «عمليات البحث عند {v}. أخطاء ذاكرة مؤقتة متكررة.»**
   "Cache miss city" is untranslatable wordplay (a place made of cache misses). Rendered
   as the plain meaning (repeated cache misses). The joke is lost; the information survives.
5. **coder — search.empty | "grep for it: \"slow\", \"dns\", \"wifi\"." → «ابحث عنه: "slow" أو "dns" أو "wifi".»**
   Kept the English search terms verbatim (they are what the user would type) but dropped
   the word «grep» — «ابحث عنه» reads better in Arabic, though «grep» would be funnier
   to a coder. Either is defensible; a reviewer who prefers the joke could use
   «grep عنه: "slow" أو "dns" أو "wifi".»
6. **emo — market.open | "The markets opened. The void opened too." → «فتحت الأسواق. وفتح الفراغ أيضًا.»**
   «الفراغ» (the emptiness/void) is the closest light, non-dark rendering of the emo
   "void" joke. It reads as a sigh, not a dark topic, but it is the most abstract line in
   the set — worth a native-speaker read.
7. **dark-ages — quiz.wrong | "Wrong, by my troth. Try again." → «خطأ، أقسم. حاول مجددًا.»**
   «by my troth» (a medieval oath) has no Arabic counterpart; «أقسم» keeps the oath
   without inventing an archaism. Slightly flat, but honest.
8. **mission — speedrunner | "One command at a time, flight." → «أمر واحد في كل مرة، يا طيران.»**
   «يا طيران» (O Flight) is a direct calque of the NASA call-sign vocative. It is the
   correct mission-control flavour, but it may read oddly to a reader unfamiliar with
   NASA comms. Alternative: drop the vocative («أمر واحد في كل مرة.») — loses voice.
9. **emo — speedrunner | "Still finishing. Patience is a vibe." → «ما زلت أنهي. الصبر مزاج.»**
   "Patience is a vibe" → «الصبر مزاج» is a loose, slightly odd rendering; «الصبر أسلوب»
   would be plainer. Kept «مزاج» for the emo shrug; flag if it reads as nonsense.
10. **dark-ages — devices.found | "{v} souls on thy network." → «{v} أرواح على شبكتك.»**
    «أرواح» (souls) for devices is the medieval joke and reads fine in Arabic, but
    double-check it doesn't feel morbid out of context — the English file's own header
    says "silly, never grim", and this line stays on the silly side.

## Things not done / open

- comfort.txt was not touched, per instructions.
- No Arabic banned-word list was supplied; I applied the English self-test regex (verified
  programmatically — zero hits) plus the spirit of the rule in Arabic (no death/killing/
  suicide/blood/war, no slurs or insults at the user, no profanity, no trademarked names
  or catchphrases). A native-speaker pass on the genz file is the main recommended
  follow-up, plus the flagged lines above.

---

## Group E

# Notes — Arabic persona translations, group E

Files: `lines-nature-ar.txt`, `lines-poetic-ar.txt`, `lines-revolution-ar.txt`,
`lines-scientist-ar.txt`, `lines-shakespeare-ar.txt`, `lines-space-saga-ar.txt`
(91 content lines each, 546 total; headers copied verbatim; keys, order and variant
counts identical to the English sources; all lines pass the `Lines.cs` self-test
rules: ≤16 words, ≤110 chars, no emoji, placeholders `{v}`/`{addr}`/`{Addr}` only
and preserved exactly, no `|`/tab, no RTL/direction-override characters, no
diacritics/tashkeel — deliberately omitted so character counts stay predictable
and the text renders cleanly).

## Shared choices

- Modern Standard Arabic throughout, pan-Arab, no dialect words. Tech terms kept
  as Arabic speakers actually say them: واي فاي، راوتر، سيرفر، بينق، تحميل،
  DNS، VPN، MTU. `AdeeboMeter` stays Latin in award lines ("جائزة Adeebo");
  acronyms and tool/protocol names (DNS, VPN, MTU) stay Latin.
- `{addr}` is rendered as a vocative ("يا {addr}") wherever the English line
  addresses the user by name; `{v}` stays inline exactly where the English
  line puts it.
- "CPU" is rendered as المعالج (the processor) rather than transliterated,
  since that is the normal Arabic word; "ping" is rendered as بينق.
- No banned content in either language: the English banned-word regex from
  `Lines.cs` was run over the Arabic files, plus a whole-word check for the
  Arabic equivalents (موت، قتل، دم، حرب، سلاح، قنبلة، غبي، أحمق، خاسر، قبيح…).
  Note: "دم" was checked as a whole word only — it also occurs harmlessly
  inside words like يستخدم، so a substring check would false-positive.

## Voice choices per persona

### Nature (nature-documentary narrator)
Calm, observational, first-person-plural "we" (نراقب، ننتظر، نعدّ) as the
documentary crew; the network is a habitat, devices are creatures, the router
is the watering hole / home territory. E.g. "هنا، في موطنها الطبيعي، تستيقظ
الشبكة" for "Here, in its natural habitat, the network awakens"; "لا مفترسات.
الفحص ناجح" for "No predators found. Checkup passed." Kept the whispered,
unhurried register — short sentences, no exclamation marks anywhere in this file.

### Poetic (lyrical)
Images of water, light, wind and threads rather than animals: "مياه ساكنة،
سماء صافية"، "كالريح"، "انقطع الخيط"، "عقد لؤلؤ صغير" for the game streak,
"الساعة أغمضت عينيها" for "The clock has closed its eyes". Slightly more
first-person singular (أصغي، أراقب، أجمع) than nature — a lone watcher, not a crew.

### Revolution (rallying, historical)
Collective address throughout: أيها المواطنون / أيها المواطن / أيها الرفيق،
الشعب، القضية، اللجنة، الجمعية، المحضر. Rallying without any violence: the
register is civic and historical (barricade → المتراس، "اثبت في موقعك" for
"hold the line"), never martial. "Burning the midnight oil" became the plain
"عمل حتى ساعة متأخرة من الليل" — the English idiom has no natural Arabic
equivalent and a literal "burning oil" would read oddly. "Knowledge is power"
kept as the well-known "المعرفة قوة".

### Scientist (precise, curious)
Lab-register vocabulary: الفرضية، المتغير، العينة، خط الأساس، اختبار ضابط،
مراجعة الأقران، ارتباط قوي. Findings are reported, not felt: "رصدت"، "لوحظ"،
"سجل الفحص". "Bufferbloat" rendered as تضخم المخزن (buffer bloat, descriptive)
rather than transliterated, since there is no established Arabic term.
"Sampling jitter" → "أعين الاهتزاز" (I measure the jitter).

### Shakespeare (classical fuṣḥā flavour)
The hard one. Aim: high classical register that a modern reader still parses
at sight — not parody, no fake "thou/thee" calques, no quoted Shakespeare.
Devices used, sparingly so the lines stay short and readable:
- Vocatives and classical address: يا {addr}، "تحدث يا {addr}. ما الذي يقلقك؟"،
  "أحسنت القول يا {addr}!"، "عمل واحد في كل مرة يا {addr}".
- Classical interjections for the English archaisms: "واحسرتاه" for "alas",
  "واأسفاه، ليس كذلك" for "Alack, 'tis not so", "مهلا، مهلا!" for "Soft, soft!".
- Older verb/noun choices where they cost nothing: التمس ("seek"), أمهلني
  ("grant me a moment"), موصد ("barred/bolted") for the closed port, الخاطر
  ("thought") in "سريع كالخاطر" for "Swift as thought", "في عهدتي" ("in my care").
- "Good morrow" → "صباح الخير" (a literal "طاب صباحك" would sound stilted
  and cost words); "Five hundred points!" → "خمسمئة نقطة!" (spelled out, as
  in the English).
- Deliberately avoided: archaic pronouns, verb endings or word order that
  would make a line hard to read, and any actual Shakespeare quotation.
  The persona is signalled by diction and address, not by grammar puzzles.

### Space saga (space opera, no film names or catchphrases)
Original space vocabulary only: السفينة، الأسطول، الجسر، الماسحات، المنارة،
المرحل، الحظيرة (hangar for port), غرفة الضغط (airlock for MTU), حقل كويكبات،
عاصفة أيونية، محرك القفز، دورة ليلية/صباحية. The user is addressed as
أيها الطيار (pilot) where the English says "pilot", and as يا {addr} elsewhere.
No Jedi/Sith/lightsaber/Force material, no "hyperspace" catchphrase — "سرعة
فضائية" instead. "Cloaking device" → "جهاز التمويه". "Holo-call" → "مكالمة
ثلاثية الأبعاد". "Hangar {v} is open" → "الحظيرة {v} مفتوحة" (port-as-hangar
metaphor kept consistently for port.open/port.closed).

## Lines I was unsure about

1. **nature / `port.closed` — "No sign of life at {v}."**
   EN: `port.closed|No sign of life at {v}.`
   AR: `port.closed|لا أثر لحياة عند {v}.`
   Why unsure: "حياة" (life) is literal and keeps the documentary voice, but
   for a closed port "لا رد" would be plainer. Kept the literal one because the
   nature persona earns its colour here and the meaning is still obvious.

2. **nature / `portal` — "This watering hole requires a sign-in."**
   EN: `portal|This watering hole requires a sign-in.`
   AR: `portal|هذا المورد يطلب تسجيل الدخول.`
   Why unsure: "المورد" (watering place / resource) is the documentary word
   for a watering hole, but in a networking context an Arabic reader may first
   read it as "the resource". The companion line ("قبل الشرب، على المسافر أن
   يسجل دخوله") carries the metaphor, so the pair works together; alone, the
   first line is slightly ambiguous.

3. **shakespeare / `busy` — "Anon, anon."**
   EN: `busy|Anon, anon.`
   AR: `busy|قريبا، قريبا.`
   Why unsure: "Anon" (soon, in a moment) has no single classical Arabic
   equivalent with the same flavour; "قريبا، قريبا" keeps the doubled
   repetition and the meaning, but loses the archaic colour. Alternatives
   considered: "عن قريب، عن قريب" (same idea, slightly more formal),
   "حالا" (too modern/brisk). Kept "قريبا، قريبا".

4. **shakespeare / `app.open` — "Good morrow, {addr}."**
   EN: `app.open|Good morrow, {addr}. I keep watch upon thy network.`
   AR: `app.open|صباح الخير يا {addr}. أحرس شبكتك.`
   Why unsure: "صباح الخير" is the plain modern greeting; the classical
   flavour here comes only from "أحرس" and the vocative. "طاب صباحك يا {addr}"
   would be more period-flavoured but is less common in everyday MSA and
   costs a word. A reviewer who wants a stronger classical opening could swap
   it in — it still fits the limits (6 words, ~30 chars).

5. **scientist / `speed.bloat` — "Bufferbloat."**
   EN: `speed.bloat|Under load, latency increases {v}. Bufferbloat.`
   AR: `speed.bloat|تحت الحمل، يزيد زمن الاستجابة {v}. تضخم المخزن.`
   Why unsure: "تضخم المخزن" (buffer bloat, literally "swelling of the buffer")
   is a descriptive coinage, not an established term; Arabic network engineers
   usually just say "bufferbloat" in Latin. But a Latin "Bufferbloat" inside
   an Arabic line would break the voice and the file's consistency, and the
   descriptive phrase is understandable. Flagging in case the project prefers
   the Latin term here.

6. **space-saga / `call.on` — "Holo-call active."**
   EN: `call.on|Holo-call active. Monitoring signal.`
   AR: `call.on|مكالمة ثلاثية الأبعاد جارية. أراقب الإشارة.`
   Why unsure: "مكالمة ثلاثية الأبعاد" (three-dimensional call) is a
   paraphrase; "هولو" transliteration would be shorter and is used by Arabic
   sci-fi fans, but it is not established MSA and would read as a brand-ish
   loanword. Kept the paraphrase; 7 words, 46 chars, well inside limits.

7. **revolution / `late.night` — "Burning the midnight oil."**
   EN: `late.night|Burning the midnight oil.`
   AR: `late.night|عمل حتى ساعة متأخرة من الليل.`
   Why unsure: the English idiom is lost entirely — the Arabic is a plain
   paraphrase ("working until a late hour of the night"). There is no natural
   Arabic idiom for working late by lamplight that fits a network app; a
   literal translation ("يحرق زيت منتصف الليل") would be nonsense. Acceptable
   loss, but noted.

8. **poetic / `update.suspect` — "Since {v}'s update, a shadow."**
   EN: `update.suspect|Since {v}'s update, a shadow.`
   AR: `update.suspect|منذ تحديث {v}، ظل خفيف.`
   Why unsure: "a shadow" is deliberately vague in English; "ظل خفيف" (a light
   shadow) adds the adjective "خفيف" to make it parse as "something is
   slightly wrong" rather than a literal shadow. A stricter rendering would be
   just "ظل" — shorter, but it reads as unfinished in Arabic. Kept the
   adjective; open to dropping it.
