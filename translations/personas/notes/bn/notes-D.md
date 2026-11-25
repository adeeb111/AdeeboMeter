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
