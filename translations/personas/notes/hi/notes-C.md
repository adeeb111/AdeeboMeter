# Notes C — Hindi translations (noir, pirate, gamer, support, bossy, stoic)

Translator group C. All six files are Devanagari Hindi with Latin tech terms kept
as Hindi speakers actually say them (Wi-Fi, DNS, VPN, ping, PC, router, server,
download, stream, broker, MTU, NAT, CPU, Bluetooth where it occurs). No line
exceeds 16 words or 110 characters; no emoji; placeholders preserved exactly.
Verified by script against the English sources (counts, key order, variant
counts, placeholders, `|`, tabs, word/char limits, the Lines.cs banned regex,
and a Hindi banned-word list). 581 content lines total.

## Voice choices

- **General register.** Everyday spoken Hindi/Hinglish, not Sanskritised or
  bookish. Second person is **तुम** in noir, pirate, gamer, bossy and stoic
  (the buddy talks like a companion, not a stranger). **Support is the
  exception: it uses आप** (आपका/आपको/कीजिए) — polite, warm, respectful,
  which is how a calm Indian helpdesk voice actually speaks. If the project
  wants one uniform pronoun everywhere, support is the file to change.
- **Noir (hard-boiled detective).** शहर/गलियाँ/सुराग/केस/गवाह vocabulary.
  "Alibi" → बेक़सूर/सफ़ाई ("घर बेक़सूर है", "PC ही अड़चन है। नेटवर्क की
  सफ़ाई है।"). "Skipped town" → शहर छोड़कर भागा. "Case closed" → केस बंद.
  "Smooth operator" → बिल्कुल चिकना काम. Kept the weary, watchful tone;
  "the city sleeps" → शहर सो रहा है.
- **Pirate (swaggering sailor).** "Matey" → साथी; "captain" kept as कप्तान;
  "Ahoy" → अहोय; "Avast" → ठहरो! Nautical kit throughout: लंगर, मचान,
  बंदरगाह, गोदाम, पीपे, मोहर, संदूक, ख़ज़ाना, doubloons kept in Latin
  (there is no natural Hindi word for it; "दुबलून" would be unreadable).
  "Shipshape" → सब जहाज़ी ढंग से ठीक है (no direct Hindi equivalent).
  "Parley" → बातचीत (the negotiation sense; a literal transliteration
  would mean nothing to most readers).
- **Gamer (gaming slang).** Latin gaming terms kept as-is, exactly as Indian
  gamers say them: GG, AFK, respawn, lobby, rage quit, carry, XP, loadout,
  build, nerf, spawn, stutter, debuff, bottleneck, rig, patch, boss fight.
  "Throwing the match" → match हरा रहा है. "Flawless route" → Flawless
  route (kept Latin; "बेदाग़ रास्ता" would lose the gamer register).
  "One more game?" → एक और game? — natural Hinglish.
- **Support (warm, helpful).** आप-form throughout (see above). Softened
  blame lines: "It's not you" → ये आप नहीं हैं; "Not something you did" →
  आपने कुछ नहीं किया. "Well looked after" → अच्छी देखभाल हो रही है.
  "Take it steady" → आराम से चलिए. Reassurance kept in every line;
  nothing sounds like a command.
- **Bossy (funny-strict, orders aimed at the gear).** Short imperatives,
  तुम-form, often two beats: "बंद करो। अभी।", "सही। अगला।", "खेलो।"
  "Home network passes. Provider fails." → घर का नेटवर्क पास। Provider
  फ़ेल। — kept the terse report-card rhythm. "Power cycle it" →
  उसकी बिजली बंद करके फिर चालू करो (no compact Hindi idiom exists).
  "Escalate it" → इसे ऊपर तक ले जाओ.
- **Stoic (Marcus Aurelius aphorisms).** Calm, impersonal where the English
  is impersonal; short sentences; "we" rendered as हम where the English
  says "we" ("जो हमारे हाथ में है", "जो हो सके, ठीक करते हैं"). "It was
  never ours to keep" → यह कभी अपना था ही नहीं (deliberately "अपना"
  rather than "हमारा" — see unsure list). "Wear it lightly" → इसे हल्के
  से पहनो. "Act, don't react" → काम करो, प्रतिक्रिया मत दो.
  "Everything has its season" → हर चीज़ का अपना समय होता है.

## Shared decisions / trade-offs

- **"Expired"** → खत्म हुआ / मियाद खत्म हुई (certificates, papers, seals).
  "Expire" as a verb has no everyday Hindi equivalent; खत्म हुआ is what
  people actually say.
- **"Doubloons"** (pirate) kept in Latin; translating it would invent a word.
- **"Packet loss / loss"** → नुकसान in most lines (packet loss → packet
  loss where the term itself matters, e.g. gamer mtr.lossy). "Loss" as
  नुकसान is slightly generic but reads naturally; "हानि" would sound
  bookish.
- **"The city sleeps / the city is quiet"** (noir) kept literally — शहर सो
  रहा है / शहर शांत है — because the noir voice needs the city as a
  character.
- **"Smooth operator"** has no Hindi idiom; rendered as बिल्कुल चिकना काम
  (smooth work), keeping the compliment.
- **Em dashes** (—) kept from the English in a few lines (pirate quiz.wrong,
  gamer game.timeup, support quiz.right, stoic inet.slow/quiz.wrong). They
  are punctuation, not emoji, and pass the self-test.
- **"9:30"** kept as-is everywhere (market open/close times).

## Lines I was unsure about

1. **noir / speed.great — "Download {v}. Smooth operator."**
   EN: `Download {v}. Smooth operator.`
   HI: `डाउनलोड {v}। बिल्कुल चिकना काम।`
   Why unsure: "smooth operator" is a compliment about a person; there is
   no Hindi idiom for it. "बिल्कुल चिकना काम" (perfectly smooth work)
   shifts the compliment from the operator to the job. Alternative: drop
   the second sentence, but that would lose a variant's flavour.
2. **noir / pc.busy — "Your PC's sweating at {v}. It knows something."**
   EN: `Your PC's sweating at {v}. It knows something.`
   HI: `तुम्हारा PC {v} पर पसीना बहा रहा है। उसे कुछ पता है।`
   Why unsure: "पसीना बहा रहा है" is a slightly odd image in Hindi (people
   say "PC गरम हो रहा है"), but the English joke is the PC "sweating" like
   a nervous suspect, so I kept the detective image rather than switching
   to overheating.
3. **pirate / all.good — "All shipshape."**
   EN: `All shipshape.`
   HI: `सब जहाज़ी ढंग से ठीक है।`
   Why unsure: "shipshape" has no Hindi equivalent; this is a paraphrase
   ("everything is fine in ship-shape fashion"). It is a bit wordy for a
   one-word line but keeps the nautical voice. Alternative: सब दुरुस्त है
   (loses the pirate flavour).
4. **pirate / game.good — "Steady winds. Go plunder."**
   EN: `Steady winds. Go plunder.`
   HI: `हवा स्थिर है। चलो, लूट पर।`
   Why unsure: "plunder" as a verb → लूट पर ("off to loot") is a playful
   paraphrase; a literal verb (लूटो) sounded like an order to rob someone
   rather than a game joke. "चलो, लूट पर" keeps it clearly playful.
5. **gamer / game.lag — "Server's fine. Your Wi-Fi is throwing the match."**
   EN: `Server's fine. Your Wi-Fi is throwing the match.`
   HI: `सर्वर ठीक है। तुम्हारा Wi-Fi match हरा रहा है।`
   Why unsure: "throwing the match" (deliberately losing) → हरा रहा है
   (is making you lose) is close but loses the "deliberate" nuance.
   "Match फेंक रहा है" would be a literal calque nobody says.
6. **support / encrypt.done — "Encrypted. Only your recipient can open it."**
   EN: `Encrypted. Only your recipient can open it.`
   HI: `Encrypt हो गया। सिर्फ़ आपका पाने वाला ही इसे खोल सकता है।`
   Why unsure: "पाने वाला" (the one who receives it) is a workaround;
   Hindi has no single everyday word for "recipient" (प्राप्तकर्ता is
   bookish). It reads fine but is slightly clunky.
7. **bossy / freeze — "Busy {v}. Clicks wait their turn."**
   EN: `Busy {v}. Clicks wait their turn.`
   HI: `व्यस्त हूँ {v}। Clicks अपनी बारी का इंतज़ार करें।`
   Why unsure: "Clicks" as a subject is odd in Hindi too, but the joke
   (clicks queuing politely) only works if "clicks" stays the subject, so
   I kept it literal.
8. **stoic / game.timeup — "Time is up. It was never ours to keep."**
   EN: `Time is up. It was never ours to keep.`
   HI: `वक़्त खत्म। यह कभी अपना था ही नहीं।`
   Why unsure: first draft used "हमारा", which contains the substring
   "मारा" — harmless under the word-boundary banned check, but a naive
   substring check could flag it. "अपना" avoids the issue entirely and
   means the same here.
9. **stoic / speed.bloat — "Fast, but the delay rises {v} when full. Moderation, even for a line."**
   EN: `Fast, but the delay rises {v} when full. Moderation, even for a line.`
   HI: `तेज़ है, पर भरने पर देरी {v} बढ़ती है। हर चीज़ में संयम, लाइन में भी।`
   Why unsure: this line is exactly 16 words — the self-test maximum. It
   passes, but has zero headroom; if a future edit adds a word it will
   fail. "Moderation, even for a line" → हर चीज़ में संयम, लाइन में भी
   is a slight expansion, needed to make the aphorism land in Hindi.
10. **stoic / inet.slow — "Your side is sound. The delay lies beyond your router - not yours to carry."**
    EN: `Your side is sound. The delay lies beyond your router - not yours to carry.`
    HI: `तुम्हारी तरफ़ सब ठीक है। देरी राउटर के आगे है — तुम्हारा बोझ नहीं।`
    Why unsure: "not yours to carry" → तुम्हारा बोझ नहीं ("not your
    burden") is a Stoic-sounding paraphrase; a literal rendering
    ("उठाना तुम्हारा काम नहीं") would be longer and less aphoristic.

## Verification

Script-checked per file (English source vs Hindi output): content line
count, header lines verbatim, key order and variant counts identical,
every {v}/{addr}/{Addr} preserved with the same count, no `|` or tab
inside translated text, every line ≤ 16 words and ≤ 110 chars, no emoji,
no match for the Lines.cs banned regex, no Hindi banned word (word-boundary
check). Result: 0 problems across all 581 lines. Per-file maxima:
noir 13 words / 57 chars; pirate 11 / 58; gamer 12 / 61; support 15 / 73;
bossy 13 / 67; stoic 16 / 74.
