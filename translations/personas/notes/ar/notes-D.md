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
