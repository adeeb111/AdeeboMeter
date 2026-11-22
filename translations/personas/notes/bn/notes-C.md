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
