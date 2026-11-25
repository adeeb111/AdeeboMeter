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
