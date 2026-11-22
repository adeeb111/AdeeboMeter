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
