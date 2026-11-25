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
