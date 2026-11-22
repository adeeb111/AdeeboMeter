# Notes — Bengali persona translations (group A1: bro, neteng)

Sources: `lines-bro.txt` (136), `lines-neteng.txt` (136) — 272 lines total, all translated.
Headers copied verbatim (both approval lines and the `# 1.1.5 batch 2` divider, kept
in the same position). Verified with a script mirroring `Lines.cs` Check(): counts,
key order, variant counts, placeholders, no `|`/tab, ≤16 words, ≤110 chars, no emoji,
no banned words. Result: 0 errors. Max 10 words / 51 chars (bro), max 9 words / 59 chars
(neteng). comfort.txt untouched.

## Voice choices

- **General register:** everyday Dhaka/Kolkata Bengali, তুমি-form throughout. Tech terms
  kept in Latin as Bengali speakers actually say them: Wi-Fi, DNS, VPN, MTU, CPU, PC,
  পিং/ping, hop, path, ডাউনলোড, স্ট্রিম, ব্রোকার, মার্কেট. AdeeboMeter, Adeebo Award,
  Windows, Stop, Changes kept untranslated.
- **Bro:** casual mate register — ব্রো on most lines, চিল করো, কী খবর, ইয়ো, সলিড,
  ভাইব, ঘোস্ট করলো. Jabs are aimed at the gear/provider, never the user. "Goated" →
  একদম সেরা (no natural Bengali equivalent); "fresh fade" → নতুন কাটের মতো ঝকঝকে;
  "fake news" (cert.expired) → ভুয়া, dropping the news-phrase; "Beast mode" kept as
  বিস্ট মোড (Dhaka gaming slang).
- **Neteng:** dry and precise, mostly short factual clauses. Kept the engineer's
  shorthand: RTT, L1-L7, RSSI, RF, AP ডিঅথ, PMTUD ব্ল্যাকহোল, SYN-ACK, RST, TTL,
  QoS, SQM, CDN, ACK/NACK. "Probes armed. Listening: none. Outbound only." → প্রোব
  তৈরি। শুনছি: কিছুই না। শুধু আউটবাউন্ড। — the no-listening-port fact survives.

## Lines I was unsure about

1. **bro / all.good** — EN: "Clean as a fresh fade, bro." → BN: নতুন কাটের মতো
   ঝকঝকে, ব্রো। Why unsure: "fade" is a specific haircut; নতুন কাট (new cut) keeps
   the fresh-haircut image but loses the fade detail. Alternative: ফেড কাটের মতো —
   more literal, less natural in Bengali.
2. **bro / wifi.drops** — EN: "Your Wi-Fi isn't a real one, bro. {v} drops." → BN:
   তোমার Wi-Fi আসল না, ব্রো। {v} বার ড্রপ। Why unsure: "isn't a real one" is a
   mate's tease about a fake/weak router; আসল না (not genuine) is the closest
   natural Bengali, but it could read as "counterfeit" rather than "useless".
3. **neteng / pc.busy** — EN: "High CPU. Network numbers may be skewed." → BN: CPU
   বেশি। নেটওয়ার্ক সংখ্যা ভুল হতে পারে। Why unsure: "skewed" (biased by load, not
   simply wrong) has no one-word Bengali equivalent; ভুল হতে পারে (may be wrong)
   is plainer and slightly stronger than the English hedge.
4. **neteng / portal** — EN: "HTTP 302 to a portal. Auth required." → BN: HTTP 302
   পোর্টালের দিকে। অথ লাগবে। Why unsure: অথ is transliterated "auth" (used in
   Dhaka tech talk); প্রমাণীকরণ লাগবে would be formal and longer. Kept the spoken form.
