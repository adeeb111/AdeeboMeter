# Notes — Arabic persona translations, group A (bro, neteng, plain, aristotle, confucius, tao)

Translator notes for the six files in this group. All 576 lines translated; verification
(counts, key order, variant counts, placeholders, ≤16 words, ≤110 chars, no emoji,
no RTL controls, no banned words) passes — see the verification summary at the end.

## Voice choices

- **General register:** Modern Standard Arabic, pan-Arab. No dialect-specific words
  (no Egyptian/Levantine/Gulf-only forms). Casual lines use MSA-friendly colloquial
  touches that read naturally everywhere: هلا، تمام، اهدأ، يا صاحبي.
- **Bro (يا صاحبي):** every line addresses the user as يا صاحبي, the Arabic "bro".
  Kept the matey, reassuring tone; slang is recreated, not translated word-for-word.
  "goated" → أسطوري; "Beast mode" → وضع الوحش; "speedrunner" → يا سريع.
  "Clean as a fresh fade, bro" → نظيفة كالجديد تماما يا صاحبي (the haircut joke
  does not travel; kept the "like-new clean" sense instead).
  "fake news" → مزيف (dropped the political catchphrase; the meaning is "fake").
- **Neteng:** dry, telegraphic, jargon kept in Latin where engineers actually write
  it that way (RTT, DNS, TCP, MTR, ARP, ICMP, SYN-ACK, PMTUD, SQM, QoS, RTP, CDN,
  AES-256, MAC, TTL, ACK/NACK, L1/L2/L1-L7, WAN, CPE, DF). Arabic glosses the
  concepts (قفزة = hop, فقدان = loss, اهتزاز = jitter, تذبذب = flap, مجس = probe,
  عنق الزجاجة = bottleneck). "flapped x{v}" kept the literal "x{v}" form.
  port.open "TCP {v} open, handshake in {v}" keeps BOTH {v} placeholders, as in English.
- **Plain:** short factual MSA, no persona colour, the fact always survives.
  "bufferbloat" kept in Latin (no established Arabic term; same as neteng).
- **Aristotle:** aphoristic, cause-and-effect, "as it ought" / mean / habit language:
  كما ينبغي، الوسط، العادة تصنع الخبير، التميز عادة. "the agora" → الأغورا
  (transliterated, not flattened to السوق, to keep the Greek colour).
  "knowledge rightly held" → معرفة ممسوكة بحق.
- **Confucius:** order, diligence, propriety, correction: في موضعه الصحيح، اجتهاد،
  صحّح، الصبر مناسب هنا. "the prudent" → العاقل (vs Aristotle's الحكيم for
  "the wise man", to keep the two personas distinct).
- **Tao:** water / flow / stillness imagery throughout: يتدفق، التيار، النهر،
  الماء الساكن، سكون، انحنِ كالقصب. "gathered without striving" → جُمعت بلا سعي.
  "Time has passed, as it does" → مر الوقت، كما يمر دائما.

## Shared term choices

- Wi-Fi → واي فاي; router → راوتر; server → سيرفر; ping → بينق; download (noun) →
  تحميل; VPN, DNS, Windows, AdeeboMeter, Adeebo Award → kept in Latin, never translated.
- "provider" → مزود الخدمة. "broker" → الوسيط. "hop" → قفزة. "gateway" → البوابة.
- {addr} appears only in the three wisdom personas' app.open lines; preserved exactly.
- No emoji, no '|' or tabs inside text, no RTL/direction-override characters.
  Arabic diacritics used sparingly (مشفّر، فُتح، مُرشَّح) only where they disambiguate
  pronunciation; they count as characters but every line stays ≤65 chars.

## Lines I was unsure about

1. **bro / all.good — "Clean as a fresh fade, bro."**
   EN: "Clean as a fresh fade, bro." → AR: نظيفة كالجديد تماما يا صاحبي.
   Why unsure: "fresh fade" is a barbershop reference (a fresh haircut). There is no
   pan-Arab equivalent that lands the same way, so I kept the "spotlessly like-new"
   sense and dropped the haircut image.
2. **bro / speed.great — "Bro, your internet is goated."**
   EN: "Bro, your internet is goated." → AR: يا صاحبي، إنترنتك أسطوري.
   Why unsure: "goated" (GOAT = greatest of all time) is youth slang; أسطوري
   ("legendary") is the closest MSA-friendly equivalent but loses the acronym joke.
3. **aristotle / market.open — "The agora opens for trade."**
   EN: "The agora opens for trade." → AR: الأغورا تفتح أبوابها للتجارة.
   Why unsure: transliterating الأغورا keeps the Greek flavour but some readers
   won't know the word; the alternative (السوق يفتح للتجارة) is clearer but loses
   the persona colour. I kept the transliteration.
4. **neteng / inet.slow — "Latency added upstream of the gateway."**
   EN: "Latency added upstream of the gateway." → AR: تأخير مضاف قبل البوابة من جهة المزود.
   Why unsure: "upstream" has no single-word Arabic equivalent in this context;
   I rendered it as "before the gateway, provider-side". The fact (delay is beyond
   the gateway) survives, but the wording is a small paraphrase.
5. **bro / encrypt.done — "Encrypted. Only your people can open it."**
   EN: "Encrypted. Only your people can open it." → AR: مشفّر. فقط من تثق بهم يفتحه.
   Why unsure: "your people" (your trusted circle) has no compact MSA equivalent;
   "those you trust" is the closest faithful rendering.
6. **tao / storm.near — "The storm comes; bend like the reed."**
   EN: "The storm comes; bend like the reed." → AR: العاصفة تأتي؛ انحنِ كالقصب.
   Why unsure: the reed-bending image is a classic Taoist image; القصب (reed/cane)
   is correct, but some readers may picture sugarcane rather than a river reed.
   The imperative انحنِ keeps the aphorism form.
7. **bro / pc.busy — "Too many tabs, bro. Your PC is tapping out."**
   EN: "Too many tabs, bro. Your PC is tapping out." → AR: تبويبات كثيرة يا صاحبي. حاسوبك يستسلم.
   Why unsure: "tapping out" (MMA submission) → يستسلم ("giving up") keeps the
   sense but loses the fight metaphor; acceptable, but flatter than the English.

## Verification summary (scripted, against the English sources + Lines.cs rules)

- bro: 136/136 lines; neteng: 136/136; plain: 136/136; aristotle: 56/56;
  confucius: 56/56; tao: 56/56. Total 576.
- Key order and per-key variant counts identical to the English files in all six.
- Header comment lines copied verbatim (including the mid-file batch-2 comment
  in bro/neteng/plain).
- Every {v} / {addr} / {Addr} preserved with the same count per line
  (including neteng port.open, which has {v} twice in both languages).
- No '|' or tab inside any translated text; UTF-8; trailing newline on all files.
- Longest line: 12 words (aristotle/tao), 65 characters (plain/aristotle) —
  well inside the 16-word / 110-char limits.
- No emoji, no surrogate characters, no RTL/direction-override characters.
- No English banned words (Lines.cs regex) and no whole-word Arabic equivalents
  of the banned list (موت، قتل، انتحار، دم، حرب، غبي...). Note: a naive substring
  check flags دم inside الخدمة / يستخدم / يخدم — those are not the word دم
  (blood); a whole-word check returns zero hits.
- comfort.txt was not read for translation and was left untouched.
