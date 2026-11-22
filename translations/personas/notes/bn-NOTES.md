# Persona translation notes — Bengali (bn)

Merged from the per-group notes written by the translator agents. 2,819 lines across 29 persona files.
These are draft translations a native speaker should sanity-check before shipping.

---

## Group A1

# Notes — Bengali persona translations (group A1: bro, neteng)

Sources: `lines-bro.txt` (136), `lines-neteng.txt` (136) — 272 lines total, all translated.
Headers copied verbatim (both approval lines and the `# 1.1.5 batch 2` divider, kept
in the same position). Verified with a script mirroring `Lines.cs` Check(): counts,
key order, variant counts, placeholders, no `|`/tab, ≤16 words, ≤110 chars, no emoji,
no banned words. Result: 0 errors. Max 10 words / 51 chars (bro), max 9 words / 59 chars
(neteng). comfort.txt untouched.

## Voice choices

- **General register:** everyday Dhaka/Kolkata Bengali, তুমি-form throughout. Tech terms
  kept in Latin as Bengali speakers actually say them: Wi-Fi, DNS, VPN, MTU, CPU, PC,
  পিং/ping, hop, path, ডাউনলোড, স্ট্রিম, ব্রোকার, মার্কেট. AdeeboMeter, Adeebo Award,
  Windows, Stop, Changes kept untranslated.
- **Bro:** casual mate register — ব্রো on most lines, চিল করো, কী খবর, ইয়ো, সলিড,
  ভাইব, ঘোস্ট করলো. Jabs are aimed at the gear/provider, never the user. "Goated" →
  একদম সেরা (no natural Bengali equivalent); "fresh fade" → নতুন কাটের মতো ঝকঝকে;
  "fake news" (cert.expired) → ভুয়া, dropping the news-phrase; "Beast mode" kept as
  বিস্ট মোড (Dhaka gaming slang).
- **Neteng:** dry and precise, mostly short factual clauses. Kept the engineer's
  shorthand: RTT, L1-L7, RSSI, RF, AP ডিঅথ, PMTUD ব্ল্যাকহোল, SYN-ACK, RST, TTL,
  QoS, SQM, CDN, ACK/NACK. "Probes armed. Listening: none. Outbound only." → প্রোব
  তৈরি। শুনছি: কিছুই না। শুধু আউটবাউন্ড। — the no-listening-port fact survives.

## Lines I was unsure about

1. **bro / all.good** — EN: "Clean as a fresh fade, bro." → BN: নতুন কাটের মতো
   ঝকঝকে, ব্রো। Why unsure: "fade" is a specific haircut; নতুন কাট (new cut) keeps
   the fresh-haircut image but loses the fade detail. Alternative: ফেড কাটের মতো —
   more literal, less natural in Bengali.
2. **bro / wifi.drops** — EN: "Your Wi-Fi isn't a real one, bro. {v} drops." → BN:
   তোমার Wi-Fi আসল না, ব্রো। {v} বার ড্রপ। Why unsure: "isn't a real one" is a
   mate's tease about a fake/weak router; আসল না (not genuine) is the closest
   natural Bengali, but it could read as "counterfeit" rather than "useless".
3. **neteng / pc.busy** — EN: "High CPU. Network numbers may be skewed." → BN: CPU
   বেশি। নেটওয়ার্ক সংখ্যা ভুল হতে পারে। Why unsure: "skewed" (biased by load, not
   simply wrong) has no one-word Bengali equivalent; ভুল হতে পারে (may be wrong)
   is plainer and slightly stronger than the English hedge.
4. **neteng / portal** — EN: "HTTP 302 to a portal. Auth required." → BN: HTTP 302
   পোর্টালের দিকে। অথ লাগবে। Why unsure: অথ is transliterated "auth" (used in
   Dhaka tech talk); প্রমাণীকরণ লাগবে would be formal and longer. Kept the spoken form.

---

## Group A2

# Bengali persona lines — notes (A2)

## Voice choices
- Register: everyday spoken Bengali (Dhaka/Kolkata), tumi-free — neutral second person
  ("আপনার", "ফিরলেন") so the lines work for any user; no bookish sadhubhasha.
- Plain: short, factual, the measured fact always kept; "you" rendered as
  "আপনার" / imperative, never as a name.
- Aristotle: reason-first aphorisms — "আগে দেখি, তারপর ভাবি, তারপর করি",
  balance/excess/habit language ("উৎকর্ষ", "অভ্যাসই দক্ষ বানায়").
- Confucius: order, duty, correction — "শৃঙ্খলা", "শোধরান", "ঠিক জায়গায়",
  "ভুল করে শুধরে নেওয়াই শেখা".
- Tao: water/flow/stillness imagery — "বয়ে চলে", "স্থির জল", "নদী",
  "নলখাগড়ার মতো নুয়ে যান"; effortlessness ("চেষ্টা ছাড়া").
- Tech terms kept/transliterated as spoken: Wi-Fi, রাউটার, পিং, DNS, VPN,
  bufferbloat, hop, CPU, PC, সার্ভার, ডাউনলোড, স্ট্রিম, ডিপ, জিটার.
  AdeeboMeter, Windows, Stop, tool names and acronyms left in English.
- "The padlock checks out" (cert.ok, plain) became "তালার যাচাই ঠিক আছে" —
  padlock rendered as "তালা" since there is no everyday Bengali word for the
  browser padlock icon.
- market.open / Aristotle "agora" became "বাজার খুলেছে বাণিজ্যের জন্য" —
  no Bengali word for agora; the trade sense is kept.
- Numbers kept in Latin digits (500, 9:30) to match the app's values.

## Unsure lines
- plain / speedrunner / "Working on your previous click first."
  → "আগে আপনার আগের ক্লিকের কাজটা করছি।"
  "আগের ক্লিকের কাজ" is slightly clumsy; "আগের অনুরোধ" (used in the next
  variant) is smoother, but the two variants needed distinct wording.
- plain / freeze / "Still working. The window is catching up ({v})."
  → "এখনো কাজ চলছে। উইন্ডো ধরতে একটু সময় নিচ্ছে ({v})।"
  "Catching up" has no neat one-word Bengali equivalent; "ধরতে সময় নিচ্ছে"
  (taking time to catch up) is the closest natural phrasing.
- tao / storm.near / "The storm comes; bend like the reed."
  → "ঝড় আসছে; নলখাগড়ার মতো নুয়ে যান।"
  "নলখাগড়া" (reed) is correct but a little literary; "বাঁশের মতো" would be
  more everyday but bamboo bends less readily than a reed, so the reed was kept.
- aristotle / dns.slow / "The fault is in the asking, not the line."
  → "দোষ চাওয়ার ধরনে, লাইনে নয়।"
  Compressed to fit the word limit; the "asking = name lookup" link is
  implied rather than spelled out.
- confucius / isp.down / "Inform them courteously, with the times."
  → "সময়সহ ভদ্রভাবে জানান।"
  "With the times" (the outage times) is compressed to "সময়সহ"; a native
  speaker may want "কখন থেকে বন্ধ, সেই সময়সহ জানান" if space allows.

## Verification
Script-checked per file: content count = English count, keys in same order
with same variant counts, all {v}/{addr} preserved, no '|' or tab in text,
every line ≤16 words and ≤110 chars, no emoji, no banned English words.
All four files: 0 errors.

---

## Group B

# Notes — Bengali persona translations (group B: epic-fantasy, chill, hype, roast, bestie)

Sources: `lines-epic-fantasy.txt` (122), `lines-chill.txt` (112), `lines-hype.txt` (112),
`lines-roast.txt` (112), `lines-bestie.txt` (105) — 563 lines total, all translated.
Headers copied verbatim (including the epic file's second approval line and the
`# 1.1.5 batch 2` divider in chill/hype/roast/bestie, kept in the same position).
Verified with a script mirroring `Lines.cs` Check(): counts, key order, variant
counts, placeholders, no `|`/tab, ≤16 words, ≤110 chars, no emoji, no banned words.
Result: 0 errors. Max 11 words, max 63 chars per line.

## Voice choices

- **General register:** everyday Dhaka/Kolkata Bengali, তুমি-form throughout (the buddy
  talks to one user, warmly and directly). Tech terms kept in Latin as Bengali
  speakers actually say them: Wi-Fi, DNS, VPN, MTU, CPU, PC, ping/পিং, hop, path,
  download/ডাউনলোড, stream/স্ট্রিম, broker/ব্রোকার, market/মার্কেট. AdeeboMeter,
  Adeebo Award, Windows kept untranslated.
- **Epic-fantasy:** a fairy-tale kingdom, not a war story. Realm = রাজ্য, castle =
  দুর্গ, watch = প্রহরা, ravens = দূত (messenger-birds, kept generic), maesters =
  জ্ঞানীরা, heralds = ঘোষকরা, kingsroad = রাজপথ. Deliberately avoided যুদ্ধ/battle
  language, death/blood words and any trademarked fantasy names or catchphrases:
  "battle" became অভিযান (quest/expedition), "winter is coming" was never used,
  "dragonfire" kept only as ড্রাগনের আগুন (generic dragon, no franchise reference).
  "Kneel, and rise a legend" → নত হও, কিংবদন্তি হয়ে ওঠো (kneeling to receive an
  honour, not to a ruler).
- **Chill:** the default-mood buddy, unhurried: ধীরে সুস্থে, চিন্তা নেই, আরাম করো,
  শান্ত সময়. "Smooth sailing" → সব মসৃণ; "Touch grass"-style slang avoided here
  entirely; "I am keeping an eye on it" → নজর রাখছি.
- **Hype:** exclamation energy on almost every line (চলো! দারুণ! একদম!), "SMOOTH" →
  একদম মসৃণ, "Lightning connection" → বিদ্যুতের মতো কানেকশন, "Night owl mode" →
  রাতজাগা মোড. "Champ" kept as চ্যাম্প (used in Dhaka gaming slang). Facts kept:
  every number/placeholder line still states the value.
- **Roast:** every jab is aimed at the gear, never the user. Router = রাউটার
  (ঢিলা দিচ্ছে, এড়িয়ে যাচ্ছে, নোট নেওয়া উচিত), Wi-Fi (কথা রাখতে পারে না, ভেজা
  নুডলসের মতো সিগন্যাল), PC (হাঁপাচ্ছে, তার সেরাটাই ধীর), provider/Windows
  (গড়িমসি করছে, আবার করেছে). No বোকা/গাধা-type insults anywhere; the two
  English lines that needle the user ("The rest is on you", "Your picks, not my
  problem") were kept as-is in meaning but softened to neutral Bengali
  (বাকিটা তোমার ওপর / তোমার পছন্দ, আমার সমস্যা নয়) since they are about the
  trade, not the person. "Touch grass?" was NOT translated literally — it would
  have been a jab at the user, so it became রাউটারও একঘেয়ে বোধ করছে (the
  router is bored too), keeping the roast on the gear.
- **Bestie:** warmest register: বন্ধু, চিন্তা কোরো না, আমি তোমার পাশে আছি, কথা
  দিলাম, তোমার জন্য গর্ব হচ্ছে. "Obsessed" (game.streak) has no natural Bengali
  equivalent in this register, so it became টানা {v} বার?! দারুণ! — same
  delighted tone, no invented meaning. "Yay" → ইয়ে! "Ooh, {v} mode! Love that
  for you" → ওহ, {v} মোড! তোমার জন্য দারুণ!

## Lines I was unsure about

1. **epic-fantasy / wifi.drops** — EN: "{v} times today the Wi-Fi bent the knee to
   the microwave." → BN: আজ {v} বার Wi-Fi মাইক্রোওয়েভের কাছে হার মানল।
   Why unsure: "bent the knee" is a fantasy-submission image; a literal নতজানু
   হওয়া would sound odd for a router in Bengali, so I used হার মানল (gave in /
   lost). The joke (microwave beats Wi-Fi) survives; the kneeling image does not.
2. **epic-fantasy / cert.expired** — EN: "A broken seal. Browsers will cry treason."
   → BN: ভাঙা সিল। ব্রাউজার বিশ্বাসঘাতকতা বলবে।
   Why unsure: "cry treason" is archaic/poetic in English; বিশ্বাসঘাতকতা বলবে
   (will call it treason) is the closest natural Bengali, but it reads slightly
   stiffer than the English. Alternative considered: ব্রাউজার মানবে না — plainer,
   but loses the fantasy flavour.
3. **epic-fantasy / speedrunner** — EN: "Easy, {addr}. Even dragons fly one at a
   time." → BN: আস্তে, {addr}। ড্রাগনও একটা একটা করে ওড়ে।
   Why unsure: "Easy" as "slow down" — আস্তে is the natural Dhaka equivalent, but
   it loses the English double meaning (easy = both "relax" and "slow down").
   Acceptable, since the second sentence carries the slow-down meaning.
4. **roast / stream.none** — EN: "Nothing streaming. Touch grass?" → BN: কিছু
   স্ট্রিম হচ্ছে না। রাউটারও একঘেয়ে বোধ করছে।
   Why unsure: deliberate rewrite, not a translation (see Roast voice choice
   above). If the reviewer wants the English joke kept, a closer version would be
   বাইরে ঘুরে আসবে? — but that aims the joke at the user, against the
   roast-the-gear rule.
5. **bestie / game.streak** — EN: "{v} in a row?! Obsessed." → BN: টানা {v} বার?!
   দারুণ!
   Why unsure: "Obsessed" as praise has no direct Bengali equivalent; দারুণ!
   keeps the excitement but drops the "I can't stop thinking about this" shade.
6. **hype / morning** — EN: "Rise and shine! Nothing broke!" → BN: ওঠো, জ্বলে ওঠো!
   কিছু ভাঙেনি!
   Why unsure: "Rise and shine" literally is ওঠো, জ্বলে ওঠো, which is understandable
   but slightly unusual in Bengali; a plainer alternative is ওঠো, সকাল হয়েছে!
   Kept the livelier version for hype energy.
7. **chill / wifi.drops** — EN: "Signal's the usual suspect." → BN: সিগন্যালই
   সন্দেহের তালিকায়।
   Why unsure: "usual suspect" is an idiom; the Bengali is literally "signal is
   on the suspect list", which is natural but loses the "usual" (again-and-again)
   shade. Alternative: সিগন্যালই বরাবরের সন্দেহভাজন — more literal, slightly
   formal. Current version chosen for everyday tone.

## Deliberate non-literal choices (all fact-preserving)

- "Ravens" → দূত everywhere in epic-fantasy (কাক would sound like an ordinary
  crow, not a royal messenger).
- "Maester" → জ্ঞানী (no Bengali word for the office; keeps the wise-councillor
  sense).
- "The force"-style and franchise wording: none present in the English, none added.
- Numbers kept in Latin digits (500, 9:30) to match the app's other Bengali text
  conventions and to stay readable inside short lines.

---

## Group C

# Notes — Bengali persona translations, group C (noir, pirate, gamer, support, bossy, stoic)

581 lines total. All six files pass a local re-implementation of the `Lines.cs`
self-test: counts, key order, variant counts, placeholders, no `|`/tab, ≤16
words, ≤110 chars, no emoji, no banned words. Longest line is 65 chars
(support `wifi.drops`, stoic `isp.down`); most words on any line is 11.

## Register decisions (all six files)

- Addressee: "তুমি/তোমার" throughout (informal-friendly, the way a buddy
  app speaks in Dhaka/Kolkata), not the formal "আপনি". Support stays warm
  in "তুমি" too — "আপনি" would have sounded like a call centre.
- Tech terms kept/transliterated as Bengali speakers say them: Wi-Fi,
  রাউটার, পিং, DNS, সার্ভার, ডাউনলোড, VPN, CPU, MTU, ISP, পোর্ট, সার্টিফিকেট.
  AdeeboMeter, Windows, iperf3, dig, MTR, NTP, SSH, TCP, UDP, HTTP, TLS,
  SMB, BGP, QUIC, DoH, DoT, mDNS, SSDP untouched. No "।" replaced by "."
  — Bengali danda is the natural sentence end.
- No dark-topic vocabulary anywhere (no খুন/মৃত্যু/রক্ত/যুদ্ধ equivalents),
  no insults aimed at the user, no profanity, no trademarked names or
  catchphrases. Noir's "case closed" / "prime suspect" register is kept
  playful, never violent.

## Voice choices per persona

### Noir (hard-boiled detective)
- Core lexicon: কেস (case), ফাইল, সাক্ষী (witness), সন্দেহভাজন (suspect),
  সূত্র (lead/trail), শহর/রাস্তা, গোয়েন্দা, চিফ.
- "The trail's gone cold" → "সূত্র ঠান্ডা হয়ে গেছে" (kept the cold-trail
  image; "ঠান্ডা" is temperature, not a dark topic).
- "Padlock's legit" → "তালা আসল।" — short, flat, detective-brief.
- "Smooth operator" → "মসৃণ চাল।" (a smooth move/operator, not a person).
- "Case of the year" award → "বছরের কেস।" "You cracked it" →
  "কেসটা ভেঙেছো।" (ভাঙা = cracked/solved, standard Bengali for solving a case).
- "Nothing playing. Just me and the rain." → "কিছু চলছে না। শুধু আমি আর
  বৃষ্টি।" — the rain image survives; it is the noir signature line.

### Pirate (swaggering sailor)
- Core lexicon: সাথী (matey), জাহাজ, সাগর, বন্দর, পাল, নোঙর, ডাঙা,
  কাকের বাসা (crow's nest — the real Bengali nautical term), খাজানা,
  সিন্দুক, মোহর (doubloons → মোহর, the classic Bengali gold coin).
- "Ahoy" → "আরে" (the "আরে" energy the brief asked for); "Avast!" →
  "আরে!" as well. "Aye" → "হ্যাঁ", "Nay" → "না" — kept plain rather than
  inventing archaic Bengali, since Bengali has no pirate dialect to lean on.
- "Captain's honours" → "ক্যাপ্টেনের সম্মান: Adeebo পুরস্কার।"
- "Go plunder" → "লুটে যাও।" — playful, no violence implied.
- "The chart keeper dozes" → "মানচিত্ররক্ষক ঝিমাচ্ছে।" (invented compound,
  transparent in Bengali).
- "Barrels too big for the hatch" → "পিপে হ্যাচের জন্য বড়।" (পিপে =
  barrel, হ্যাচ kept as a loanword; both are understood in nautical Bengali).

### Gamer (gaming slang)
- Kept the English gamer lexicon Bengali gamers actually use in Latin
  script: GG, AFK, XP, Alt-F4, NAT, লবি, র‍্যাংকড, রেসপোন, লোডআউট,
  বাফারব্লোট, ল্যাগ স্পাইক, ফ্রেম ড্রপ, বস ফাইট, নার্ফ, ক্যারি, ডিবাফ.
- "র‍্যাংকড" is spelled with a ZWJ (র + ্ + য + ZWJ) — that is the normal
  Bengali spelling of the য-ফলা conjunct, not an emoji or control trick;
  the self-test does not flag it.
- "rage quit" → "রেজ কুইট" (transliterated; Bengali gamers say exactly this).
- "That's a rage quit" / "throwing the match" → "ম্যাচ হারাচ্ছে" (losing
  the match on purpose — "throwing" has no clean Bengali gamer equivalent).
- "Download the whole game library" kept literal; "Patch day won't hurt"
  → "প্যাচ ডে কষ্ট দেবে না।"
- "The network's carrying its weight today" → "নেটওয়ার্ক আজ নিজের কাজ
  করছে।" (idiom replaced with plain Bengali; a literal weight metaphor
  would read oddly).

### Support (warm, helpful)
- Softening words throughout: "ঠিক আছে", "চিন্তার কিছু নেই", "একসঙ্গে
  বুঝে নেব", "আমি আছি", "তোমার দোষ নয়" — the reassurance is the voice.
- "I saved what happened before each drop" → "প্রতিবার ছিটকানোর আগে কী
  হয়েছিল, রেখেছি।" (longest line in the set, 65 chars, 10 words).
- "Calls may stutter during big downloads. Your router can help with
  that." → "বড় ডাউনলোডের সময় কল আটকাতে পারে। রাউটার এতে সাহায্য করতে
  পারে।" — "stutter" rendered as আটকানো (getting stuck), the natural
  Bengali description of a stuttering call.
- "Not this time, and that's how learning works." → "এবার হয়নি, আর
  এভাবেই শেখা হয়।" — gentle, no consolation-prize tone.

### Bossy (funny-strict, orders aimed at the gear)
- Short imperatives, often verbless: "থামো।", "দাঁড়াও।", "খেলো।",
  "পাঠাও।", "এগোও।" Orders address the user and the gear interchangeably,
  as in English ("Network, behave" → "নেটওয়ার্ক, ভদ্রভাবে চলো।").
- "Home network passes. Provider fails." → "বাড়ির নেটওয়ার্ক পাস।
  প্রোভাইডার ফেল।" — kept the clipped report-card style.
- "Power cycle it" → "পাওয়ার সাইকেল করো।" (loan phrase; Bengali has no
  shorter native equivalent and users say this).
- "Queue respected. Last one first." → "সারি মানা হচ্ছে। আগেরটা আগে।"
- "At ease" (award line) → "বিশ্রাম নাও।" — military flavour softened
  to a plain order, since Bengali has no "at ease" idiom.
- "Escalate it" → "উপরে জানাও।" (tell the people above).

### Stoic (Marcus Aurelius aphorisms, calm and wise)
- Short, balanced sentences; "তোমার বোঝা নয়" (not yours to carry),
  "ধৈর্যের কোনো দাম নেই" (patience costs nothing), "প্রতিটি পছন্দের দাম
  আছে" (every choice has its cost).
- "Let us attend to what is in our power: this network." → "যা আমাদের
  হাতে, সেটাই দেখি: এই নেটওয়ার্ক।" — the dichotomy of control, in plain
  Bengali.
- "Everything has its season" (cert.expired) → "সবকিছুর একটা সময় আছে।"
- "Time is up. It was never ours to keep." → "সময় শেষ। এটা কখনো আমাদের
  ছিল না।"
- "Act, don't react" (market.open) → "কাজ করো, প্রতিক্রিয়া নয়।"
- "The market sleeps until 9:30. So may you." → "বাজার ঘুমাচ্ছে সকাল
  ৯:৩০ পর্যন্ত। তুমিও ঘুমাতে পারো।"
- "Wear it lightly" (award) → "হালকাভাবে পরো।" — literal, and it works
  in Bengali as advice about not clinging to honours.

## Lines I was unsure about

1. noir `game.good` — EN: "Ping {v}. Clean getaway." → BN: "পিং {v}।
   পরিষ্কার পালানো।" — "Clean getaway" is a heist/detective image; "পরিষ্কার
   পালানো" (a clean escape) keeps it, but "পালানো" (fleeing) could read
   slightly odd for a ping result. Alternative: "পিং {v}। ঝামেলা ছাড়াই।"
   (without any trouble). Kept the getaway image as the more noir choice.

2. noir `cert.expired` — EN: "Expired since {v}. Sloppy work, their
   side." → BN: "{v} থেকে মেয়াদ শেষ। ঢিলেঢালা কাজ, ওদের দিকে।" —
   "ঢিলেঢালা কাজ" (sloppy/loose work) is the closest Bengali idiom, but it
   is a little softer than "sloppy". Alternative: "অগোছালো কাজ, ওদের দিকে।"

3. pirate `speed.bloat` — EN: "Swift, until the hold fills. {v} of extra
   delay." → BN: "দ্রুত, খোল ভরলে থামে। {v} বাড়তি দেরি।" — "খোল" (hold)
   is correct nautical Bengali, but a non-sailor reader may not know it.
   The surrounding pirate lines make the meaning clear; kept it.

4. pirate `busy` — EN: "Swabbing the packets." → BN: "প্যাকেট ধুয়ে সাফ
   করছি।" — "swabbing" (mopping a deck) has no Bengali naval verb; "ধুয়ে
   সাফ করা" (washing clean) is the plain equivalent. Loses the deck-mop
   joke, keeps the meaning.

5. gamer `game.lag` — EN: "Server's fine. Your Wi-Fi is throwing the
   match." → BN: "সার্ভার ঠিক। তোমার Wi-Fi ম্যাচ হারাচ্ছে।" — "throwing"
   (deliberately losing) has no Bengali gamer word; "হারাচ্ছে" (is losing)
   drops the deliberate-sabotage nuance. A native gamer reviewer may
   prefer "ম্যাচ ফেলছে" — flagging for review.

6. gamer `update.suspect` — EN: "It broke after the update on {v}. Patch
   nerfed it." → BN: "{v}-এর আপডেটের পর ভেঙেছে। প্যাচ নার্ফ করেছে।" —
   "নার্ফ" is the transliteration Bengali gamers use, but it is jargon;
   a non-gamer may not know it. In the gamer persona that is acceptable,
   but worth a native-speaker check.

7. support `wifi.drops` — EN: "Your Wi-Fi has been unsteady. I saved what
   happened before each drop." → BN: "তোমার Wi-Fi অস্থির ছিল। প্রতিবার
   ছিটকানোর আগে কী হয়েছিল, রেখেছি।" — "রেখেছি" (I kept/saved it) is
   slightly vague without an object; "সংরক্ষণ করেছি" would be clearer but
   stiffer. Kept the warmer, shorter form.

8. bossy `mtr.lossy` — EN: "Hop {v} drops packets. Escalate it." → BN:
   "হপ {v} প্যাকেট ফেলছে। উপরে জানাও।" — "উপরে জানাও" (tell those above)
   is colloquial for escalate; "এসকেলেট করো" would be the loanword
   alternative. Chose Bengali; either is defensible.

9. stoic `freeze` — EN: "A pause ({v}). Patience costs nothing." → BN:
   "একটু বিরতি ({v})। ধৈর্যের কোনো দাম নেই।" — "costs nothing" rendered
   as "কোনো দাম নেই" (costs nothing / is priceless-ambiguous in Bengali it
   reads as "has no price", i.e. free). The intended meaning survives, but
   "ধৈর্য ধরতে কিছু লাগে না" would be more literal. Kept the aphoristic
   version.

10. stoic `net.none` — EN: "No network. Annoyance changes nothing. Check
    the cable or the Wi-Fi." → BN: "নেটওয়ার্ক নেই। বিরক্তি কিছু বদলায় না।
    কেবল বা Wi-Fi দেখো।" — "বিরক্তি কিছু বদলায় না" (annoyance changes
    nothing) is faithful; "বিরক্তি" is slightly formal. Alternative:
    "রাগ করলে কিছু বদলাবে না।" (getting angry changes nothing) — more
    everyday, arguably more stoic in tone. Flagging for a native-speaker call.

---

## Group D

# Notes — Bengali persona translations (Group D: emo, coder, genz, dark-ages, industrial, mission)

Sources: `lines-emo.txt` (98 lines), `lines-coder.txt`, `lines-genz.txt`,
`lines-dark-ages.txt`, `lines-industrial.txt`, `lines-mission.txt` (91 each) —
553 content lines total. Headers copied verbatim; key order, key names and
variant counts preserved exactly. `comfort.txt` was not touched.
Self-test rules from `src/Lines.cs` lines 210–240 were replicated in a Python
check: per file, content count = English count; keys in same order with same
variant counts; every `{v}` / `{addr}` / `{Addr}` preserved; no `|` or tab
inside translated text; every line ≤16 words and ≤110 chars; no emoji;
no English banned words (the `Banned` regex from Lines.cs). Result: 0 problems.
Max words per file 7–9; max chars 48–55 — well inside the limits.

## Shared choices

- Register: everyday spoken Bengali (Dhaka/Kolkata), তুমি throughout, Bengali
  দাঁড়ি (।) for sentence ends. Latin digits kept for literal values
  (500 points, 0%, status 200) as in the English.
- Tech words kept/transliterated as Bengali speakers actually say them:
  Wi-Fi, রাউটার, পিং, DNS, সার্ভার, ডাউনলোড, VPN, CPU, PC, পোর্ট-নয় —
  port numbers stay as `{v}`. Never translated: AdeeboMeter, Windows,
  iperf3/dig/MTR/NTP/SSH/TCP/UDP/HTTP/TLS/SMB/BGP/QUIC/DoH/DoT/mDNS/SSDP
  (none of these occur in these six files, but the rule was applied),
  acronyms, file names.
- No dark topics, slurs, profanity, trademarked names or catchphrases anywhere.
  Note on one false alarm in my own checker: the Bengali words বিরক্ত /
  বিরক্তিকর ("annoyed/annoying") contain the substring রক্ত ("blood") but are
  unrelated words; verified by word-token check, they are fine and were kept.

## Voice choices per persona

### Emo (`lines-emo-bn.txt`)
Melodramatic, moody, sighing — but light, never dark, per the header.
Signature moves: "আমিও, PC। আমিও।", "কেমন যেন অদ্ভুত লাগছে",
"সন্দেহজনক রকম শান্ত", "ধৈর্যও একটা ভাইব", "তোমার Wi-Fi-র মন খারাপ"
(for "going through something"). Sadness is aimed at the network/PC,
never at the user. "The void opened too" → "শূন্যতাও খুলেছে" (kept
deliberately flat/whimsical rather than grim); "Time's gone. Like
everything." → "সময় শেষ। সবকিছুর মতোই।" (wistful, not dark).

### Coder (`lines-coder-bn.txt`)
Programmer humour kept in code terms, Bengali framing around them:
"Hello, world" kept verbatim; 404, grep, cache miss ("ক্যাশ মিসের শহর"),
"শিপ করো" (ship it), "বাইসেক্ট ওখানেই", "অ্যাসারশন ফেল", "টাইমআউট",
"স্ট্যাটাস 200", "গারবেজ কালেক্ট করছি". Jokes that only work in English
were kept in English where the term is the joke (404, grep, Hello world);
the rest is Bengali ("বটলনেক: PC, নেটওয়ার্ক নয়").

### Gen-Z (`lines-genz-bn.txt`)
Current Dhaka/Kolkata youth slang, Bangla-English mix: "হেই", "ফ্র",
"লো-কি এলিট", "রেয়ার W", "বড় L", "কুকড", "ঘোস্ট করেছে", "মিড",
"সাস", "মেইন ক্যারেক্টার এনার্জি", "আইকনিক", "চিল করছি", "ব্রব" (brb),
"সিন করে রেখেছে" (left on read — "seen" → সিন), "খেয়ে ফেলেছ, একদম
পরিষ্কার" (ate that, no crumbs), "বেস্টি". **This slang dates fast:**
W/L, মিড, ফ্র, কুকড, ঘোস্ট — re-check in a year; the file will sound
2025–26. "Speedrun any%?" kept as "স্পিডরান এনিওয়ান%?" since the
English term is the joke. Kept the slang light and never aimed at the user.

### Dark Ages (`lines-dark-ages-bn.txt`)
Medieval Bengali voice, silly never grim: "শোনো হে!", দুর্গ / রাজ্য /
প্রজা / লিপিকর / দূত / ঘোষক / সরাইখানা / মোমের সিল / উত্তোলন সেতু /
স্বর্ণমুদ্রা / নাইট উপাধি. Deliberately avoided archaic তব/তাহার-style
bookishness — modern তোমার/তুমি with medieval nouns, so it reads
playful, not like a textbook. "By my troth" → "সত্যি বলছি" (no archaic
oath exists naturally in Bengali). "Bandits, surely" → "নিশ্চয়ই ডাকাত"
(kept as a joke, not a threat). "Souls on thy network" → "তোমার
নেটওয়ার্কে {v} প্রাণী" (প্রাণী = living beings; "আত্মা" would sound
ghostly/dark, so avoided).

### Industrial (`lines-industrial-bn.txt`)
Factory floor: বয়লার, গেজ, পাইপ, বাষ্প, চাকা, গিয়ার, লিভার, ভালভ,
তাঁত, শিফট, ফোরম্যান, স্প্যানার, শক্ত বাক্স (strongbox), খাতা (ledger).
"Threw a spanner in" → "{v} আপডেটটা স্প্যানার ছুড়েছে" (kept the idiom
literally — it works in Bengali). "The works hum" → "কারখানা সুন্দর
গুনগুন করছে". Short, clanking sentences; no extra adjectives.

### Mission (`lines-mission-bn.txt`)
Calm NASA-style procedure: মিশন কন্ট্রোল, ফ্লাইট, টেলিমেট্রি নমিনাল,
সব সিস্টেম গো / নো-গো, স্ট্যান্ডবাই, ডাউনরেঞ্জে, চেকলিস্ট, কনসোল,
আপলিংক, ভয়েস লুপ, পেলোড. "Go/no-go" kept as গো / নো-গো (the callouts
are the voice; translating them would lose the register). "Flight" kept
as ফ্লাইট as a callsign. Everything is a status report or a recommendation,
never excitement.

## Lines I was unsure about

1. emo / `market.open` — EN: "The markets opened. The void opened too."
   BN: "বাজার খুলেছে। শূন্যতাও খুলেছে।"
   Why unsure: "the void" has no natural Bengali equivalent here; শূন্যতা
   (emptiness/void) is the closest, but it reads slightly more philosophical
   than the English joke. Kept flat so it stays whimsical, not dark.
2. emo / `game.lag` (2nd variant) — EN: "Server's fine. Your Wi-Fi is going
   through something."
   BN: "সার্ভার ঠিক আছে। তোমার Wi-Fi-র মন খারাপ।"
   Why unsure: "going through something" is idiomatic; "মন খারাপ" (in a bad
   mood) is the emo-voice equivalent, but it personifies the Wi-Fi more
   strongly than the English does. Felt right for emo; flagging anyway.
3. genz / `quiz.right` — EN: "Ate that, no crumbs."
   BN: "খেয়ে ফেলেছ, একদম পরিষ্কার।"
   Why unsure: "ate" slang has no direct Bengali equivalent; this renders it
   as "you devoured it, totally clean" — understandable, but the English
   slang punch is softened. "নো ক্রাম্বস" transliterated would be more
   faithful to the slang but less understandable; chose clarity.
4. genz / `speedrunner` — EN: "Speedrun any%? Still on the last one."
   BN: "স্পিডরান এনিওয়ান%? আগেরটাতেই আছি।"
   Why unsure: "any%" transliterated as এনিওয়ান% is opaque to anyone who
   doesn't know speedrunning; kept because the audience for the joke is
   exactly people who do. A plainer fallback: "স্পিডরান? আগেরটাতেই আছি।"
5. dark-ages / `devices.found` — EN: "{v} souls on thy network."
   BN: "তোমার নেটওয়ার্কে {v} প্রাণী।"
   Why unsure: প্রাণী (creature/being) is slightly odd for devices; "আত্মা"
   (soul) was rejected as ghostly/dark. Alternative: "{v} জন প্রাণ" — no,
   worse. প্রাণী is the least-bad playful option.
6. coder / `app.open` — EN: "Hello, world. Monitoring {addr}'s network."
   BN: "Hello, world। {addr}-র নেটওয়ার্ক মনিটর করছি।"
   Why unsure: kept "Hello, world" in English with a Bengali দাঁড়ি after it.
   Translating it ("হ্যালো, দুনিয়া") would kill the joke; keeping it is the
   standard Bengali-developer rendering. Flagging only because it is the one
   English sentence fragment in the Bengali files.
7. mission / `pc.busy` (2nd variant) — EN: "Guidance: the PC is the
   constraint, not comms."
   BN: "নির্দেশনা: বাধা PC, কমস নয়।"
   Why unsure: "কমস" (comms) is a transliteration with no established Bengali
   use; "যোগাযোগ" would be clearer but loses the mission-control clipped
   register. Kept কমস for voice consistency with the other mission lines.
8. emo / `storm.near` — EN: "A storm is coming. Fitting."
   BN: "ঝড় আসছে। মানানসই।"
   Why unsure: "Fitting" is doing a lot of deadpan work in one word;
   মানানসই (fitting/appropriate) carries it, but the joke lands a little
   quieter in Bengali. No better single word found.

---

## Group E

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
