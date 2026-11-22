# Persona translation notes — Hindi (hi)

Merged from the per-group notes written by the translator agents. 2,819 lines across 29 persona files.
These are draft translations a native speaker should sanity-check before shipping.

---

## Group A

# Hindi translation notes — personas group A (bro, neteng, plain, aristotle, confucius, tao)

576 content lines translated (136 + 136 + 136 + 56 + 56 + 56). All files verified
against the Lines.cs self-test rules: counts, key order and variant counts match
the English files exactly; every {v} / {addr} preserved with the same count;
no '|' or tab inside any line; every line ≤ 16 words and ≤ 110 characters
(UTF-16 length); no emoji; no banned English words; headers copied verbatim.

## Voice choices

- **General register:** natural spoken Hindi/Hinglish, Devanagari script, Roman
  kept only where Hindi speakers actually say it in Roman (Wi-Fi, DNS, VPN,
  TCP, MTU, bufferbloat, SQM, MTR, RTP, AES-256, ACK/NACK, TTL, CDN, ET).
  Tech nouns are transliterated, not translated: राउटर, पिंग, सर्टिफिकेट,
  ब्रोकर, स्ट्रीम, डाउनलोड, एन्क्रिप्ट/डिक्रिप्ट. AdeeboMeter, Windows and all
  command/tool names left untouched.
- **Bro:** "भाई" on almost every line, masculine intimate "तू" (matches the
  English bro register; this is the one persona that uses तू). Slang kept
  Hinglish-flavoured: चिल, सॉलिड, टॉप, बीस्ट मोड, लेजेंड. "goated" has no
  Hindi equivalent, rendered as "एकदम टॉप है".
- **Neteng:** driest of the six — mostly noun-phrase fragments, English terms
  left in English (RTT nominal, 0% loss, no flaps; SYN-ACK; PMTUD blackhole).
  Hindi used only for glue words (है, चेक करो, ज़्यादा, रुके हैं). The
  English "x{v}" in "wlan flapped x{v}" is preserved as-is (the "x" is not
  a placeholder).
- **Plain:** "तुम" throughout, simplest everyday Hindi, fact first, no
  ornament. "Only the right person" rendered gender-neutral as "सही व्यक्ति".
- **Aristotle:** formal-but-plain Hindi, aphoristic second sentence where the
  English has one ("यही इसकी श्रेष्ठता है", "श्रेष्ठता एक आदत है"). "Excellence
  is a habit" kept verbatim in spirit both times it occurs. "agora" kept as
  transliteration "अगोरा" (see unsure list).
- **Confucius:** order/duty vocabulary — व्यवस्था, क्रम, रिवाज़, सतर्क, धैर्य,
  अभ्यास. "Inform them courteously, with the times" rendered as "समय के साथ
  विनम्रता से उन्हें बताओ" (i.e. tell them politely, with the times noted).
- **Tao:** water/flow vocabulary throughout — बहता है, धारा, नदी, शांति,
  बिना ज़ोर. "Sealed." rendered as "सील हो गया।" to keep the one-word punch.

## Lines I was unsure about

| Key | English | My Hindi | Why unsure |
|---|---|---|---|
| bro / all.good (5th) | Clean as a fresh fade, bro. | भाई, नेटवर्क एकदम साफ़ है। | Dropped the haircut "fade" metaphor entirely — it would not land in Hindi and there is no equivalent idiom. The line loses its joke; it is now just "network is spotless". |
| bro / speed.great (2nd) | Bro, your internet is goated. | भाई, तेरा इंटरनेट एकदम टॉप है। | "Goated" (GOAT slang) has no Hindi equivalent; "एकदम टॉप" is the closest natural praise. Slightly weaker than the English. |
| bro / game.streak | {v} straight, bro. Beast mode. | भाई, {v} लगातार, बीस्ट मोड। | Kept "बीस्ट मोड" as transliteration; a pure-Hindi rendering ("राक्षस मोड") would sound wrong. Fine, but it leans on the reader knowing the English phrase. |
| aristotle / market.open | The agora opens for trade. | अगोरा व्यापार के लिए खुल गया है। | "Agora" transliterated as "अगोरा" — most Hindi readers will not know the word; "बाज़ार" would be clearer but loses the Greek reference that marks the persona. Kept the reference. |
| aristotle / mtu.small | Large packets are lost at {v}. The path's limit is the mean we must respect. | बड़े पैकेट {v} पर खो जाते हैं। रास्ते की सीमा का सम्मान करो। | "The mean" (golden mean) dropped — the full rendering was 18 words, over the 16-word limit. The line keeps "respect the path's limit" but loses the Aristotle-specific "mean" idea. |
| aristotle / late.night | It is late. Rest serves the reason that works tomorrow. | देर हो गई है। आराम कल काम करने वाली बुद्धि की सेवा करता है। | Faithful but clunky in Hindi; "बुद्धि की सेवा करता है" is a literal rendering of "serves the reason". Reads a bit stiff. |
| tao / cert.expired | The certificate ended on {v}. All things return to their root. | सर्टिफिकेट {v} को समाप्त हुआ। सब कुछ अपनी जड़ लौटता है। | "अपनी जड़ लौटता है" is slightly awkward Hindi; alternative: "सब कुछ अपनी जड़ की ओर लौटता है" (same word count, smoother). Either is acceptable. |
| confucius / portal | This Wi-Fi requires a sign-in first. Observe the custom of the house. | इस Wi-Fi को पहले साइन इन चाहिए। घर का रिवाज़ निभाओ। | "घर का रिवाज़ निभाओ" (follow the custom of the house) is a guess at the intended sense — the English is deliberately archaic/ritual; the Hindi is a natural equivalent but not literal. |
| neteng / wifi.drops (1st) | wlan flapped x{v}. Check RSSI and roaming. | wlan x{v} बार flap हुआ। RSSI और roaming चेक करो। | "x{v}" kept verbatim as in English; if the app ever renders this, the "x" prefix reads fine in Hindi too. |
| plain / encrypt.done (2nd) | Locked. Only the right person can open it. | लॉक हो गई। सिर्फ़ सही व्यक्ति ही खोल सकता है। | "सही व्यक्ति" (right person) is gender-neutral but slightly formal for plain voice; "सिर्फ़ वही खोल सकता है जिसे खोलना चाहिए" was too long. |

## Deliberate trade-offs

- Bro uses "तू" and the philosophers/plain use "तुम" — intentional, per persona.
- Where the English line is a fragment ("Quiet.", "Sealed.", "Correct."),
  the Hindi is also a fragment, not a padded full sentence.
- Em dashes (—) kept in a few neteng/aristotle lines; they count as one
  whitespace-separated word each and all such lines are well under 16 words.
- comfort.txt was not touched.

---

## Group B

# Hindi translation notes — personas group B (epic-fantasy, chill, hype, roast, bestie)

Translator: subagent session 295bcca4, 2026-11-17. All 563 lines translated and
verified against the Lines.cs self-test rules (see verification summary at the end).

## Voice choices

### General
- Register: everyday spoken Hindi/Hinglish, "तुम" throughout (the English buddy
  is informal and close; "आप" would sound like a bank helpline). Tech words
  stay in Roman as Hindi speakers actually say them: Wi-Fi, राउटर, पिंग, DNS,
  VPN, MTU, CPU, PC, डाउनलोड, सर्टिफिकेट, ब्राउज़र. AdeeboMeter, Windows and
  tool/acronym names untouched.
- No line exceeds 14 words or 63 characters, so there is comfortable headroom
  under the 16-word / 110-char limits even if a {v} value runs long.

### Epic-fantasy (lines-epic-fantasy-hi.txt)
- Quest voice built from a small fixed lexicon so it stays consistent:
  राज्य (realm), किला (castle/keep), पहरा (watch), दूत (raven/messenger),
  मीनार (tower), द्वारपाल (gatekeeper), मोहर (seal), गुप्त सुरंग (secret tunnel),
  शाही सड़क (kingsroad), दरबार (court), परिषद (council), महाज्ञानी (maester).
- "Ravens" became दूत (messenger), not कौआ — a crow would sound comic, not epic.
- "Maester" became महाज्ञानी (great scholar); there is no good one-word Hindi
  equivalent and transliterating "मेस्टर" would mean nothing to most readers.
- "Kingsroad" became शाही सड़क; "gatekeeper" became द्वारपाल (a real Hindi word
  for a gate guard, keeps the fantasy register).
- Deliberately avoided: any GoT/Star Wars names or catchphrases, and any dark
  words — no मौत/खून/युद्ध anywhere. "The night is dark and full of dropouts"
  was reworked to "रात अंधेरी है, रुकावटें कई हैं" (the night is dark, dropouts
  are many) so it keeps the meaning without echoing the TV line. "Bent the knee"
  became "माइक्रोवेव के आगे हार गया" (lost to the microwave) for the same reason.
- "Souls" (devices.found) became प्राणी (living beings) — आत्माएँ would have
  sounded ghostly.

### Chill (lines-chill-hi.txt)
- The default mood: plain, unhurried Hinglish. "Smooth sailing" → "सब स्मूद है",
  "No stress" → "कोई टेंशन नहीं", "Carry on" → "चलते रहो". "Blinked out" →
  झपका (flickered), which is what people actually say about Wi-Fi here.
- "Grab the snacks" kept literal ("स्नैक्स ले आओ") — it is a natural Hinglish line.

### Hype (lines-hype-hi.txt)
- Energy carried by "!" on nearly every line plus चलो, वाह, "नाइट आउल मोड",
  "चैंपियन", "लेजेंड". A few English words kept in Roman for punch where Hindi
  speakers actually shout them: SMOOTH, FAST, LIVE ("बाज़ार LIVE हैं!").
- "Go get them!" → "जाओ, उन्हें हराओ!" (go, beat them) — direct and sporty.
- "Bell's ringing" → "घंटी बज रही है" (the bell is ringing) — the stock-market
  opening bell, kept literal.

### Roast (lines-roast-hi.txt)
- The gear is always the target, never the user: राउटर, Wi-Fi, PC, Windows,
  प्रोवाइडर get mocked ("तुम्हारा राउटर जवाब देना बंद कर गया। हमेशा की तरह।",
  "उस DNS सर्वर को कॉफ़ी चाहिए", "Windows ने फिर कर दिखाया").
- User-facing lines were softened where the English is borderline: "Shocking, I
  know" (quiz.right) → "हैरानी है, मुझे पता है" (surprising, I know) — teasing,
  not insulting. "Who are you?" (game.streak) kept as "तुम कौन हो?" — playful
  amazement, not an insult.
- "Touch grass?" → "बाहर घूम आओ?" (go out for a walk?) — the English meme has
  no Hindi equivalent; this keeps the joke (go outside) without a literal
  grass-touching line that would baffle readers.
- "Wet noodle" kept literal ("गीले नूडल जितनी सिग्नल ताकत") — the image survives
  and is funny in Hindi too.
- "VPN tax" → "VPN टैक्स" (transliterated, not "कर") — "टैक्स" is the word
  Hindi speakers use for a surcharge like this.

### Bestie (lines-bestie-hi.txt)
- Warm and on the user's side: "मैं हूँ ना" energy via "तुम्हारे लिए यहीं हूँ",
  "मैं संभालती हूँ", "तुम पर गर्व है", "साथ में करेंगे", "बेस्टी" kept as
  बेस्टी in the two lines where the English says "bestie".
- "Yay" has no Hindi word: rendered as "ये!" ("कहीं कोई नुकसान नहीं। ये!",
  "+{v} पॉइंट! ये!") — the closest cheerful interjection.
- "Obsessed" (game.streak) → "मैं दीवानी हूँ" (I'm crazy about it) — the
  fannish sense, not the clinical one.

## Gender decision (flagged, not hidden)
- Hindi verbs force a gender for the buddy. I chose masculine ("रहा हूँ",
  "कर रहा हूँ") for chill, hype, roast and epic-fantasy (epic has no
  first-person verbs, so it is unaffected), and feminine ("रही हूँ", "कर रही हूँ",
  "देख रही हूँ") for bestie, where the warm female-friend voice reads most
  naturally in Hindi. If Adeeb wants one consistent buddy gender across all
  personas, bestie is the only file to change (search "रही" → "रहा" etc.).

## Lines I was unsure about
1. epic-fantasy / wifi.drops — EN: "The signal fell {v} times. The night is dark and full of dropouts."
   HI: "सिग्नल {v} बार गिरा। रात अंधेरी है, रुकावटें कई हैं।"
   Why: the English is a Game of Thrones riff ("the night is dark and full of
   terrors"); I kept the night/dark image but dropped the catchphrase echo.
   If the riff is acceptable, a closer version would be "रात अंधेरी है और
   रुकावटों से भरी है" — but I would not recommend it.
2. epic-fantasy / award — EN: "The Adeebo Award. Kneel, and rise a legend."
   HI: "Adeebo Award। झुको, और महान बनकर उठो।"
   Why: "kneel and rise" is a knighting image; "झुको" (bow) is the closest
   natural Hindi verb, but it loses the ceremony slightly. Alternative:
   "घुटने टेको, और महान बनकर उठो।" — more literal, slightly stiffer.
3. roast / stream.none — EN: "Nothing streaming. Touch grass?"
   HI: "कुछ स्ट्रीम नहीं हो रहा। बाहर घूम आओ?"
   Why: see "Touch grass?" note above — no Hindi equivalent of the meme; this
   keeps the meaning (go outside) but loses the meme recognition.
4. bestie / game.streak — EN: "{v} in a row?! Obsessed."
   HI: "लगातार {v}?! मैं दीवानी हूँ।"
   Why: "Obsessed" as a compliment is very English-internet; "मैं दीवानी हूँ"
   captures the tone but a reader could take it literally. Alternative:
   "लगातार {v}?! कमाल है।" (safer, less bestie-voice).
5. bestie / mtr.clean — EN: "No loss anywhere. Yay."
   HI: "कहीं कोई नुकसान नहीं। ये!"
   Why: "ये!" as "Yay" is my best guess at a Hindi cheer; it is short and
   cheerful, but if it reads odd, "वाह!" would be the safer swap.
6. hype / speed.great — EN: "Download {v}! Your line is flying!"
   HI: "डाउनलोड {v}! तुम्हारी लाइन उड़ रही है!"
   Why: "उड़ रही है" (is flying) is a natural Hindi boast for speed; kept it,
   but note it is idiom, not literal flight.
7. chill / wifi.drops — EN: "Your Wi-Fi blinked out {v} times. Signal's the usual suspect."
   HI: "तुम्हारा Wi-Fi {v} बार झपका। सिग्नल ही शक के घेरे में है।"
   Why: "usual suspect" became "शक के घेरे में है" (is under suspicion) — the
   police-show idiom does not exist in Hindi; this is the natural equivalent.

## Verification (all passed, 0 problems)
- Per-file content counts match English exactly: epic-fantasy 122, chill 112,
  hype 112, roast 112, bestie 105 — total 563. Key order and variant counts
  identical; header comment lines copied verbatim (including the mid-file
  "# 1.1.5 batch 2" comment in chill/hype/roast/bestie).
- Every {addr}/{v} placeholder preserved exactly (same set, same count per line).
- No '|' or tab inside any translated text; UTF-8 with trailing newline.
- Every line ≤ 16 words and ≤ 110 chars (actual maxima: roast 14 words,
  roast 63 chars; all other files lower).
- No emoji (checked the 0x2600–0x27BF range and surrogates); no banned English
  words matched by the Lines.cs Banned regex; no dark/slur/profanity/trademark
  content in the Hindi (checked by hand while translating).
- comfort.txt was not touched. No source files were modified.

---

## Group C

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

---

## Group D

# Notes — Hindi personas, group D (emo, coder, genz, dark-ages, industrial, mission)

Translator: subagent, 2026-11-17. Sources read-only; `comfort.txt` untouched.
Verification: all 6 files re-checked against `Lines.cs` Check() rules — 553/553 lines,
keys in identical order with identical variant counts, all `{v}`/`{addr}`/`{Addr}`
preserved, no `|`/tab inside text, max 14 words and max 60 chars per line,
no emoji, no banned-word regex hit. UTF-8, trailing newline, header copied verbatim.

## Shared choices
- Everyday spoken Hindi, mostly Devanagari script. Tech words kept as Hindi speakers
  actually say them: Wi-Fi, राउटर, पिंग, DNS, VPN, सर्वर, डाउनलोड, MTU, TLS, HTTP.
  AdeeboMeter, Windows, tool/acronym names never translated.
- No dark topics anywhere, even where the English leans that way (emo especially):
  no मृत्यु/खून/युद्ध or equivalents; "gone/lost" lines were softened to
  "बीत गया / चला गया / गायब हो जाते हैं" (vanish = गायब, not death).
- No insults aimed at the user in any persona, including genz ("cooked", "on read"
  kept as teasing, never stupid/idiot equivalents).
- Placeholders stay in Latin script exactly as in English (`{v}`, `{addr}`).

## Voice choices per persona

### emo — melodramatic, moody, self-aware
First-person, sighing Hindi: "मैं यहाँ हूँ", "समझ नहीं आ रहा", "हम सब ऐसे ही हैं".
Kept the persona's habit of turning a network fact into a feeling
("शांत है। शक होने वाली शांति।", "सर्वर ठीक है। तुम्हारा Wi-Fi किसी दौर से गुज़र रहा है।").
Deliberately light, never dark — the English "Like everything" / "It won't last"
lines are wistful, not grim.

### coder — programmer humour, Hinglish
Short, clipped, code-flavoured: "प्रोसेस शुरू", "बिल्ड ग्रीन", "नो एरर। नो वार्निंग।",
"grep करके देखो", "Assertion फेल", "Bisect points there" → "वहीं bisect करो".
English code words (build, commit, cache miss, timeout, 404, Status 200) kept in
Latin script because that is how Indian developers actually speak/write them.
**Trap found:** the English line "Hello, world" itself trips the app's banned-word
regex — `\bhell` prefix-matches "Hello". Translated as "हैलो, world" so the Hindi
file passes the self-test the English file would fail.

### genz — current Hinglish youth slang (DATES FAST — see below)
Heavy Roman-script slang mixed into Devanagari sentences, as actually spoken:
"so back", "lowkey elite", "Rare W", "cooked", "ghost कर दिया", "flaky", "fr",
"ate किया", "on it हूँ", "brb", "big L", "on read", "sus", "Locked in", "Main character energy".
Untranslatable catchphrases left in English rather than forced into Hindi:
"Ate that, no crumbs" and "Main character energy" are kept verbatim (with a Hindi
sentence around them where the English had one). **This file will date fastest:**
"ate / no crumbs / lowkey / fr / W / L / ghost / cooked / mid / vibe check" are
2024–2026 slang; expect a refresh pass within a year or two. "bestie" kept as-is.

### dark-ages — medieval voice, silly never grim
Archaic-register Hindi: "सुनो!", "हे", "दुर्ग", "राज्य", "प्रजा", "साधु", "लेखक",
"संदेशवाहक", "ढिंढोरची", "मोमबत्ती", "शुभ प्रभात", "मेरी सौगंध".
Modern tech words are the joke — they stay modern ("Wi-Fi", "DNS", "MTU", "फाइबर")
against the medieval frame, exactly as in English ("Witchcraft? No, good fibre").
"Thy" rendered as "तुम्हारा/तुम्हारे" (respectful, not the archaic "तेरा", which
would sound rude in modern Hindi). "Peasants" softened to "प्रजा" (subjects/people)
— "किसान" would be literal but loses the court joke; "प्रजा" keeps it.
"Bandits, surely" → "डाकू होंगे, ज़रूर" (no violence implied, just the joke).
"Enemy castle" → "दुश्मन के दुर्ग" — दुश्मन is mild, no war language used anywhere.

### industrial — factory / steam / machine voice
Workshop Hindi: भट्टी, गेज, पाइप, करघा, बेल्ट, वाल्व, तिजोरी, शिफ्ट, फोरमैन, सीटी.
"Works" → "कारखाना", "looms" → "करघे", "spanner" → "पेंच" ("पेंच फँसा दिया").
Kept the persona's concrete, physical metaphors: leaks, pressure, steam, crates/chutes.

### mission — mission control, calm NASA-style procedure
Flat, procedural Hindi: "मिशन कंट्रोल ऑनलाइन है", "सभी सिस्टम go हैं",
"टेलीमेट्री सामान्य है", "स्टैंडबाय", "कन्फर्म", "नेगेटिव", "no-go".
"Flight" (the addressee) kept transliterated as "फ्लाइट" — there is no natural
Hindi equivalent for the NASA call-sign, and translating it as "उड़ान" would read
wrong. "Go / no-go" also kept in Latin script, as in real Indian mission-control
/ aviation usage. "Downrange" → "आगे की ओर" (no exact Hindi term; deliberately plain).

## Lines I was unsure about

| key | English | My Hindi | Why unsure |
|---|---|---|---|
| emo / market.open | The markets opened. The void opened too. | बाज़ार खुल गए। खालीपन भी खुल गया। | "The void" is emo-absurd; खालीपन (emptiness) is the closest natural Hindi, but loses the joke's punch. Alternative: "सूनापन भी खुल गया" — no better. |
| emo / game.timeup | Time's gone. Like everything. | समय बीत गया। सब कुछ की तरह। | Softened on purpose (no dark wording allowed); "सब कुछ की तरह" is vague in Hindi. Acceptable, but the wistfulness is thinner than English. |
| emo / speedrunner | Still finishing. Patience is a vibe. | अभी खत्म कर रहा हूँ। सब्र भी एक vibe है। | Kept "vibe" in Latin script — natural in Hinglish, but a purer-Hindi reader may prefer "सब्र भी एक अंदाज़ है". I kept vibe for the emo voice. |
| emo / storm.near | A storm is coming. Fitting. | तूफ़ान आ रहा है। माहौल के हिसाब से है। | "Fitting" (i.e. fitting my mood) is hard in one short line; "माहौल के हिसाब से है" is the closest I got. |
| genz / quiz.right | Ate that, no crumbs. | Ate that, no crumbs। | Left entirely in English. Any Hindi rendering ("खा गए, कोई कसर नहीं") kills the slang. Will date fast; flag for review when slang moves on. |
| genz / award | You got the Adeebo Award. Main character energy. | तुम्हें Adeebo Award मिला। Main character energy। | Same: "Main character energy" has no Hindi equivalent that lands. Kept verbatim deliberately. |
| genz / inet.slow | Home network ate. The internet past it did not. | होम नेटवर्क ने ate किया। आगे का इंटरनेट नहीं। | "Ate" as slang ("did great") is untranslatable; "ने ate किया" is how Hinglish speakers actually say it, but it may confuse a non-slang reader. |
| dark-ages / quiz.wrong | Wrong, by my troth. Try again. | गलत है, मेरी सौगंध। फिर कोशिश करो। | "By my troth" (medieval oath) → "मेरी सौगंध" is formal/archaic but more solemn than the English joke. "मेरी कसम" would be more everyday; सौगंध chosen for the old-time register. |
| dark-ages / speed.great | Download {v}. Witchcraft? No, good fibre. | डाउनलोड {v}। जादू? नहीं, अच्छा फाइबर है। | "Witchcraft" → "जादू" works, but the English joke lands on "good fibre" as the punchline; in Hindi the punchline is flatter. No better short option found. |
| dark-ages / all.good | The peasants report no problems. PC, Wi-Fi, internet fine. | प्रजा कहती है कोई दिक्कत नहीं। PC, Wi-Fi, इंटरनेट ठीक हैं। | "Peasants" → "प्रजा" softens the class joke; literal "किसान" would be odd (farmers reporting on Wi-Fi). Judgment call. |
| mission / app.open | Flight, we're go for network monitoring. | फ्लाइट, नेटवर्क मॉनिटरिंग के लिए हम go हैं। | "Flight" as a call-sign has no Hindi form; transliteration is the only option, but a Hindi-only reader won't know why they're being called फ्लाइट. |
| mission / mtr.lossy (2nd) | Loss begins at hop {v}. Downrange. | लॉस हॉप {v} से शुरू होता है। आगे की ओर। | "Downrange" has no Hindi equivalent; "आगे की ओर" is plain filler. Could also be dropped, but variant count must stay exact, so a short tail was needed. |
| coder / app.open | Hello, world. Monitoring {addr}'s network. | हैलो, world। {addr} का नेटवर्क मॉनिटर हो रहा है। | See trap above: Latin "Hello" fails the banned regex (`hell`). हैलो in Devanagari passes. Worth flagging to the parent: the English source line itself would fail Check(). |
| industrial / update.suspect | The {v} update threw a spanner in. | {v} के अपडेट ने पेंच फँसा दिया। | "Threw a spanner in (the works)" → "पेंच फँसा दिया" is idiomatic enough, but "spanner" itself (रिंच/पाना) is lost. Fine for the voice. |

## Not translated
- `comfort.txt` — left entirely alone, per task.
- Nothing in the GPT drop or any repo was modified (read-only).
- Other files already present in `corrected/personas/hi/` (bro, chill, nature, poetic,
  revolution, epic-fantasy, scientist, shakespeare, space-saga, hype) are other
  agents' work; not touched, not verified here.

---

## Group E

# Hindi persona translations — notes (group E: nature, poetic, revolution, scientist, shakespeare, space-saga)

546 lines total (91 per file). All verified against the Lines.cs self-test rules:
91 content lines per file, keys in the same order with the same variant counts,
headers copied verbatim, every `{v}` / `{addr}` preserved exactly, no `|` or tab
inside any translated text, every line ≤16 words and ≤110 characters
(actual maxima: 13 words, 58 characters), no emoji, no banned words.
Tech terms kept/transliterated as Hindi speakers say them: Wi-Fi, राउटर, पिंग,
DNS, VPN, PC, CPU, MTU, सर्टिफिकेट, डाउनलोड, सर्वर. AdeeboMeter and all
command/tool/acronym names left untouched.

## Voice choices

- **Nature** — calm documentary narrator, present-tense observation
  ("देखिए"-style without the word itself, to save space): "यहाँ, अपने घर में,
  नेटवर्क जाग उठा है।" Animals kept where the English used them
  (बाज़, चीता, स्लॉथ, झुंड, शिकारी); "habitat" rendered as घर / तंत्र
  rather than the bookish "आवास".
- **Poetic** — lyrical, image-led, everyday Hindi: "तारें धीरे गुनगुना रही
  हैं", "ठहरा पानी, साफ़ आसमान", "मोतियों की छोटी माला". Kept the
  metaphors (wind, pearls, key-in-lock) rather than explaining them.
- **Revolution** — rallying, collective, "नागरिको!" / "साथी" address,
  "जनता", "सभा", "मोर्चा", "परिषद". Deliberately no war/blood language:
  "barricade" → मोर्चा, "struggle" → संघर्ष, "burning the midnight oil" →
  "आधी रात तक काम जारी" (no literal burning). "Liberty, equality, low
  latency" kept as a triad: "आज़ादी, बराबरी, कम लेटेंसी।"
- **Scientist** — clipped, precise, measurement vocabulary: परिणाम,
  परिकल्पना, नमूना, कारक, नियंत्रण जाँच, आँकड़ों में. "Bufferbloat" kept
  as बफ़रब्लोट (as in lang-hi.txt); "hypothesis confirmed" → "परिकल्पना
  सही निकली".
- **Shakespeare** — old-style शुद्ध Hindi, archaic high register, as
  briefed: तू / तेरा / हे {addr} throughout, with Sanskritised forms
  (सुप्रभात, कुशल, वेगवान, मंदता, विघ्न, श्रम, मंत्रणा, भोर, प्रहर,
  उत्तीर्ण, दुर्भाग्य, अपरिवर्तित). "Thy" is always तेरा, never आपका,
  and verbs use तू-forms (कर, आ, बोल, ठहर, बढ़). "Alack, 'tis not so"
  → "हाय, ऐसा नहीं है।" "Anon, anon" → "शीघ्र, शीघ्र।" "Five hundred
  points" spelled out as "पाँच सौ पॉइंट!" to match the English spelling
  it out. Deliberately NOT parody: no thee/thou-style Hinglish, no
  forced rhyming, and the technical facts (CPU {v}, MTU {v}, DNS {v})
  stay plain so the line still works as a status message.
  Note: तू can read over-familiar in modern Hindi if the user is a
  stranger; that is the briefed register (तू-style forms), so I kept it
  consistent rather than mixing तू and आप. If the app ever wants a
  softer archaic address, the only change needed is तू→तुम/आप forms.
- **Space-saga** — generic space opera, no film names or catchphrases:
  जहाज़, बेड़ा, पुल (bridge), बीकन, रिले, सेक्टर, पायलट, स्कैनर,
  हैंगर, पतवार, ट्रांसपोंडर, होलो-कॉल. "Hyperspace speed" → "प्रकाश से
  भी तेज़ गति" (avoided a transliterated "हाइपरस्पेस"); "hyperdrive
  engaged" → "हाइपरड्राइव चालू" (हाइपरड्राइव is the established Hindi
  gaming term). "Cloaking device" → "छिपने का यंत्र".

## Hardest 3 lines

1. **shakespeare / speedrunner** — "Soft, soft! I am yet upon the last."
   Archaic gentleness + "still on the previous task" in ≤16 words and
   high register. Rendered: "धीरे, धीरे! मैं अब भी पिछले कार्य में हूँ।"
   ("धीरे, धीरे" carries the "soft, soft" plea; "पिछले कार्य में हूँ"
   keeps the meaning without a modern "प्रोसेस चल रहा है".)
2. **revolution / all.good** — "Liberty, equality, low latency. All is well."
   The joke is the triad ending in a tech term. Rendered:
   "आज़ादी, बराबरी, कम लेटेंसी। सब ठीक है।" — kept the three-beat
   structure and the Latin "लेटेंसी" so the punch lands the same way.
3. **nature / quiz.wrong** — "A miss. Even the cheetah misses sometimes."
   The cheetah idiom had to survive in Hindi without sounding like a
   proverb. Rendered: "चूक गए। चीता भी कभी-कभी चूकता है।" — direct,
   keeps the animal voice, and "चूकता है" echoes "चूक गए".

## Unsure lines

- **nature / dns.slow** — "DNS, slow as a sloth: {v}."
  → "DNS, सुस्त जैसे स्लॉथ: {v}।" Sloths are not a familiar animal in
  India; "स्लॉथ" is a transliteration most readers will get from
  nature documentaries, but "आलसी जानवर" would be clearer and loses
  the specific image. Kept स्लॉथ for the documentary voice.
- **poetic / game.timeup** — "The clock has closed its eyes."
  → "घड़ी ने आँखें बंद कर लीं।" Literal and lyrical in Hindi, but
  slightly odd as a status line; alternative would be "समय पूरा हुआ।"
  Kept the image since the persona is poetic.
- **revolution / dns.slow** — "Lookups take {v}. Bureaucracy."
  → "खोज में {v} लगता है। दफ़्तरी देरी।" "Bureaucracy" has no short
  Hindi punch; "दफ़्तरी देरी" (office-style delay) is my coinage and
  may read as slightly invented. Alternative: "अफ़सरशाही।" (more
  standard, but harsher and longer in tone).
- **revolution / speed.bloat** — "Fast, but busy lines slow everyone {v}. Unfair."
  → "तेज़ है, पर व्यस्त लाइन सबको {v} धीमा करती है। नाइंसाफ़ी।"
  "नाइंसाफ़ी" (injustice) is colloquial Urdu-Hindi; "अन्याय" would be
  more शुद्ध but feels heavier for a one-word tag. Kept नाइंसाफ़ी.
- **shakespeare / call.on** — "Thou art in counsel. I watch the call."
  → "तू मंत्रणा में है। मैं कॉल देख रहा हूँ।" "मंत्रणा" is archaic and
  correct, but uncommon; "सलाह-मशविरा" would be more everyday and
  less in-register. Kept मंत्रणा.
- **shakespeare / vpn.slow** — "The masque costs {v}."
  → "इस भेस की कीमत {v} है।" "Masque" (masked ball / disguise) has no
  Hindi equivalent; भेस (disguise) is the closest, and matches the
  previous line's "भेस में तू यात्रा कर रहा है।"
- **space-saga / speed.great** — "{v} down. Hyperspace speed."
  → "{v} डाउन। प्रकाश से भी तेज़ गति।" "प्रकाश से भी तेज़" is
  physically impossible (faster than light) and a reader may notice;
  it is the standard space-opera boast, so kept. Alternative:
  "अंतरिक्ष-गति।"
- **space-saga / busy** — "Calculating jump... {v}"
  → "छलाँग गिन रहा हूँ... {v}" "Jump" as a noun (a faster-than-light
  hop) has no settled Hindi term; "छलाँग" is used consistently in
  this file (also speedrunner "एक बार में एक छलाँग", mtr.clean
  "पूरी छलाँग में कोई हानि नहीं").
- **scientist / speed.bloat** — "Under load, latency increases {v}. Bufferbloat."
  → "लोड में लेटेंसी {v} बढ़ती है। बफ़रब्लोट।" Kept बफ़रब्लोट
  untranslated (matches lang-hi.txt usage) rather than coining a
  Hindi term for bufferbloat.
