# Bengali persona translations — translator notes (group: nature, poetic, revolution, scientist, shakespeare, space-saga)

Translator: Muse (subagent). 546 lines total, 91 per file. All six files pass the
Lines.cs self-test rules (verified by script, not by eye): 91/91 lines each, keys in
the same order with the same variant counts, headers copied verbatim, every
`{v}`/`{addr}` preserved exactly, no `|` or tab inside any line, max 10 words and
max 62 characters per line (limits are 16 words / 110 chars), no emoji, no banned
English words. Sources were read-only; nothing in the GPT drop was modified.
comfort.txt was not touched.

## Shared choices

- Register: everyday Dhaka/Kolkata চলতি ভাষা for five personas; সাধু ভাষা only for
  shakespeare (see below). Second person is তুমি throughout (the buddy is familiar,
  not formal আপনি and not intimate তুই).
- Tech terms kept/transliterated as Bengali speakers actually say them: Wi-Fi,
  রাউটার, পিং, DNS, সার্ভার, ডাউনলোড, VPN, CPU, MTU, PC, লেটেন্সি, বাফারব্লোট,
  জিটার, ক্যাপটিভ পোর্টাল, সার্টিফিকেট, এনক্রিপ্ট/ডিক্রিপ্ট. AdeeboMeter, Windows,
  tool/acronym names and file names never translated.
- "500 points" stays as Latin digits `500 পয়েন্ট` (matches the English line and
  the UI's own number style); only the shakespeare award line spells it out as
  পাঁচশত, because the archaic register demanded it.
- "The Adeebo Award" is kept as `Adeebo Award` in every file (a proper name).
- The banned-word spirit was applied in Bengali too: no যুদ্ধ/মৃত্যু/রক্ত, no insults
  aimed at the user, no profanity, no trademarked names or catchphrases.
- Punctuation uses the Bengali দাঁড়ি (।) for full stops; em dashes (—) are kept
  where the English used them.

## Voice choices per persona

### nature — nature-documentary narrator
Calm, hushed, third-person observer: "আমরা দেখছি", "পর্যবেক্ষণ", habitat/herd
imagery carried over (আবাস, দল, গুহা, জলাশয়, পথিক). Animals kept literal
(বাজপাখি for falcon, চিতা for cheetah, শ্লথ for sloth). "The watcher stays" →
"পাহারাদার রইল" — the watcher is rendered as a watchman, not a passive eye.

### poetic — lyrical
Soft imagery, water/light/air metaphors (স্রোত, সন্ধ্যার আলো, বাতাস, মুক্তোর মালা).
Short, breath-like lines; several two-word lines kept ("তালাবদ্ধ।"). "The clock has
closed its eyes" → "ঘড়ি চোখ বুজেছে" — kept the personification, which is the
whole point of the line.

### revolution — rallying, historical
"citizen" → নাগরিক, "comrade" → সাথি (not কমরেড — সাথি is the warmer, more
natural Bengali rallying address), "the people" → জনগণ, "the cause" → উদ্দেশ্য.
Committee/assembly/council vocabulary kept (কমিটি, সভা, পরিষদ, কার্যবিবরণী).
Deliberately avoided যুদ্ধ (war) anywhere, including "hold the line" →
"লাইন ধরে রাখো" (kept as the network line, not a battle line). "Burning the
midnight oil" is an English idiom with no Bengali twin, so it became the plain
"মধ্যরাত পর্যন্ত জেগে কাজ" — see unsure list.

### scientist — precise, curious
Method vocabulary throughout: পরীক্ষা, অনুমান, তথ্য, চলক, নমুনা, পর্যবেক্ষণ,
নথিভুক্ত, ভিত্তিরেখা (baseline), "control test" → নিয়ন্ত্রণ পরীক্ষা, "variable
isolated" → চলক আলাদা করা হলো. "Bufferbloat" kept untranslated as বাফারব্লোট
(the standard term among Bengali network folk); "jitter" → জিটার; "learning
curve" → শেখার বক্ররেখা.

### shakespeare — সাধু ভাষা (sadhu bhasha)
The hard one. Consistent archaic register, but kept readable — the grammar of
Tagore-era prose, not a parody:
- তব for "thy" (তব PC, তব Wi-Fi, তব নেটওয়ার্ক, তব গৃহ, তব রাউটার); তুমি for
  "thou" (তুমি বিচ্ছিন্ন হইয়াছ, তুমি ফিরিয়া আসিয়াছ); হে for vocative
  ("হে {addr}").
- Verb endings in -ইতেছে/-ইল/-ইয়াছে: করিতেছে, হইল, হইয়াছে, আসিয়াছ, কহিয়াছ,
  দেখিতেছি, রহিলাম, মিলিল, পাইল, ঘটিল.
- নাই / নহে for negation ("কোনো উপদ্রব নাই", "নেটওয়ার্কে নহে"); কহ for "speak";
  কহিবার for "to tell"; হইতে for "from"; কিংবা for "or"; শীঘ্রই for "anon";
  "Good morrow" → শুভ প্রভাত (both occurrences).
- Modern tech words are left modern (PC, Wi-Fi, DNS, VPN, CPU, MTU, সার্টিফিকেট)
  — sadhu-ising them would be unreadable; the register lives in the verbs,
  pronouns and particles around them.
- "Alack, 'tis not so" → "হায়, তাহা নহে।" "Time hath run its course" →
  "কাল পূর্ণ হইয়াছে।" (কাল = time, in the classical sense.)
- Deliberately did NOT use "সাধু, সাধু" for "Well done" (points.earned) — it
  would read as a joke about the register itself. Used "উত্তম কার্য।" instead.
- "Five hundred points" is spelled out as পাঁচশত পয়েন্ট (the only file where
  the number is not Latin digits), for register consistency.

### space-saga — space opera (no Star Wars names or catchphrases)
Ship/bridge vocabulary throughout: জাহাজ, ব্রিজ, বহর, সেক্টর, রিলে, বীকন,
হ্যাঙ্গার, এয়ারলক, হাল (hull), ঢাল (shields), ট্রান্সপন্ডার. "Hyperspace" →
হাইপারস্পেস, "hyperdrive" → হাইপারড্রাইভ, "jump" → জাম্প, "ion storm" → আয়ন
ঝড়, "holo-call" → হলো-কল — all transliterated, since Bengali sci-fi readers
know these words in English form. The user is addressed as পাইলট where the
English does ("Easy, pilot", "Rank rising, pilot"). No Jedi/Sith/Force/lightsaber
or any film reference anywhere; "the fleet salutes you" → "বহর তোমাকে অভিবাদন
জানাচ্ছে" is generic naval language, not a quote.

## Lines I was unsure about

1. nature / `dns.slow` — EN: "DNS, slow as a sloth: {v}."
   BN: "DNS, শ্লথের মতো ধীর: {v}।"
   Why unsure: শ্লথ is the correct Bengali word for the sloth animal, but it is
   rare in everyday speech (most speakers would say "আলসে প্রাণী" or just "ধীর").
   Kept শ্লথ because the animal simile is the joke; a native reviewer may prefer
   "কচ্ছপের মতো" (tortoise), which is instantly understood but loses the exact
   animal.

2. revolution / `late.night` — EN: "Burning the midnight oil."
   BN: "মধ্যরাত পর্যন্ত জেগে কাজ।"
   Why unsure: the English idiom has no Bengali equivalent; the literal image
   (তেল পোড়ানো) would be nonsense. The translation is plain and loses the
   period flavour. "মধ্যরাতের প্রদীপ জ্বালিয়ে" was considered and rejected as
   too flowery for a rallying voice.

3. revolution / `idle` — EN: "All quiet on the barricade."
   BN: "ব্যারিকেডে সব শান্ত।"
   Why unsure: ব্যারিকেড is a direct transliteration; Bengali has no native word
   for a revolutionary barricade, and the word is understood mainly through
   history/politics. Alternative "মোর্চায় সব শান্ত" (মোর্চা = front/rampart)
   is more Bengali but shifts the image from street barricade to battlefield,
   which the persona note ("never violent") argues against. Kept ব্যারিকেড.

4. shakespeare / `busy` — EN: "Anon, anon."
   BN: "শীঘ্রই, শীঘ্রই।"
   Why unsure: "anon" in Shakespeare means "soon/presently", and শীঘ্রই is the
   right sadhu equivalent, but the doubled repetition reads slightly flat in
   Bengali where the English repetition is playful. Considered "এই আসিতেছি,
   এই আসিতেছি" — rejected, it sounds like someone arriving, not working.

5. space-saga / `game.good` — EN: "Ping {v}. Cleared for launch."
   BN: "পিং {v}। উড্ডয়নের ছাড়পত্র মিলেছে।"
   Why unsure: উড্ডয়ন (take-off/flight) is a formal word; "লঞ্চের ছাড়পত্র"
   would be closer to the space idiom but লঞ্চ in Bengali most often means a
   river launch (boat). উড্ডয়ন avoids the boat confusion at the cost of
   sounding slightly literary.

6. scientist / `speed.bloat` — EN: "Under load, latency increases {v}. Bufferbloat."
   BN: "চাপে লেটেন্সি বাড়ে {v}। বাফারব্লোট।"
   Why unsure: বাফারব্লোট is transliterated, not translated — there is no
   established Bengali term. A descriptive alternative ("বাফার জমে যাওয়া")
   would explain more but would break the scientist's terse labelling style.
   A native reviewer should confirm Bengali network users recognise বাফারব্লোট.

7. poetic / `search.empty` — EN: "Name what troubles you."
   BN: "যা ভোগাচ্ছে, তার নাম বলো।"
   Why unsure: ভোগাচ্ছে ("is afflicting/tormenting you") is stronger than
   "troubles" and slightly medical in tone; "যা কষ্ট দিচ্ছে" is softer but
   longer and less lyrical. Kept ভোগাচ্ছে for the poetic bite.
