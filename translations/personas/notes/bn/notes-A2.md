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
