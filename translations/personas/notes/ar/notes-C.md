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
