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
