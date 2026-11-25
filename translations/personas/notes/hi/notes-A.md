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
