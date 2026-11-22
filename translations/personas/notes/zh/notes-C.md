# Notes C — Simplified Chinese translations (noir, pirate, gamer, support, bossy, stoic)

Translator group C. All 581 lines translated (105 noir + 105 pirate + 105 gamer + 112 support + 98 bossy + 56 stoic). Verified against `Lines.cs` self-test rules (word/char limits, placeholders, banned words, emoji) — all six files pass. `comfort.txt` untouched.

## Voice choices

- **Noir （硬汉侦探）**: 1930s hard-boiled detective in mainland Chinese. The network is "这座城市", the user is "老兄", problems are "案子/线索/卷宗", the ISP is always "嫌疑人", evidence is "不在场证明". Short, flat, world-weary sentences. "Smooth operator" → "手法利落". "Case closed" → "结案". "The chief noticed" → "局长注意到了".
- **Pirate （海盗）**: swaggering sailor, not a literal "yer be" imitation (that reads as bad grammar in Chinese). Sea vocabulary does the work: 啊哈， 起锚， 瞭望手， 桅杆， 船舱， 风暴， 港口， 宝箱， 金币. Addressed the user as "老伙计" and "船长". "doubloons" → "金币" throughout (no natural Chinese word for doubloon; 金币 is what Chinese games use). "Raise the colours" → "升起战旗". "Avast!" → "慢着！".
- **Gamer （玩家）**: mainland gaming slang as actually used: GG, 排位， 掉线， 掉帧， 挂机， 重生， 连胜， 带飞， 延迟尖峰， 速通玩家， 全绿， 单机模式. "Full send" → "全力冲". "debuffs" → "减益效果". "XP" kept as "经验" in `+{v} 经验` (points.earned) — Chinese games say 经验, not XP, in running text; the literal "+{v} XP" would also have been acceptable. "Alt-F4" kept verbatim.
- **Support （暖心客服）**: warm, calm, reassuring; never needy. Frequent softeners: "没关系", "不是你的问题", "我们一起", "慢慢来", "我会一直在这儿". Advice is phrased as gentle suggestion ("通常有帮助", "可能有帮助"), never an order. "Well earned" → "实至名归". "Proud of the work" → "为你的努力感到高兴" (kept first-person-free so it reads as the buddy speaking).
- **Bossy （严厉上司）**: short imperatives aimed at the gear, funny-strict. Sentence-final orders: "立刻", "现在", "等着", "待命", "解散", "稍息", "下一个". The router/network is addressed directly ("网络，给我老实点", "路由器，保持住"). "Queue respected" → "按顺序来，上一个优先". "Dismissed" → "解散". "At ease" → "稍息". No insults — strictness is aimed at devices and queues, never at the user.
- **Stoic （斯多葛）**: Marcus Aurelius in spirit, no quotations. Calm, aphoristic, modern Chinese with a light classical cadence: "当下", "万物皆有其时", "每个选择都有代价", "休息也是一种责任", "行动，不要反应". Control-dichotomy lines kept explicit ("力所能及的事", "不必由你承担", "不必烦恼"). "Wear it lightly" → "轻轻松松地戴着它".

## Shared choices

- Tech terms kept as Chinese users see them: Wi-Fi, 路由器， ping, DNS, VPN, MTU, 证书， 端口， 运营商， 券商. Tool/acronym names never translated: iperf3, dig, MTR, NTP, SSH, TCP, UDP, HTTP, TLS, SMB, BGP, QUIC, DoH, DoT, mDNS, SSDP, AdeeboMeter, Windows, NAT, Boss, GG.
- "provider/ISP" → "运营商" everywhere. "broker" → "券商" (trading context). "captive portal" → "强制门户" (the standard Chinese term) in noir/gamer/bossy; pirate/support paraphrase it as "港口 Wi-Fi 要先登录" / "打开登录页" to fit their voice.
- "hop" → "第 {v} 跳" everywhere. "packet loss" → "丢包". "bufferbloat" → "缓冲膨胀" (gamer, kept technical) vs paraphrase in support ("负载一大延迟就多").
- Placeholders: every `{v}` / `{addr}` preserved exactly; no line gained or lost one. `{Addr}` does not occur in these six files.
- Ellipsis "…" in English became "……" (Chinese full-width ellipsis) in busy/freeze lines.
- No emoji anywhere; no `|` or tab inside any translated line; longest line is 30 characters (limit 110); max 5 whitespace-separated words (limit 16).

## Lines I was unsure about

1. **noir / stream.none** — EN: "Nothing playing. Just me and the rain." → ZH: "没在播，只有我和雨声。" The rain is in the English, so I kept it, but it is the only place rain appears in the whole persona; if the buddy has no rain imagery elsewhere it may read as a stray line. Alternative: "没在播，只有我在盯着。" (drops the rain).
2. **noir / speed.great** — EN: "Download {v}. Smooth operator." → ZH: "下载 {v}，手法利落。" "Smooth operator" is a compliment to the connection; "手法利落" (clean technique) is the closest hard-boiled equivalent, but it slightly shifts the praise from the line to the downloader. Alternative: "下载 {v}，顺滑得很。"
3. **pirate / game.good** — EN: "Steady winds. Go plunder." → ZH: "风稳得很，放手去闯吧。" I dropped "plunder" （掠夺） because it reads harsher in Chinese than the playful English and could sound like encouraging wrongdoing; "放手去闯" keeps the swagger. If the literal joke is wanted: "风稳得很，去掠夺吧。"
4. **pirate / points.earned** — EN: "+{v} doubloons." → ZH: "+{v} 金币。" There is no natural Chinese word for "doubloon"; 金币 is the standard game term. "达布隆" (transliteration) would be accurate but unreadable to most users.
5. **gamer / game.lag** — EN: "Server's fine. Your Wi-Fi is throwing the match." → ZH: "服务器没问题，是你的 Wi-Fi 在拖后腿。" "Throwing the match" (deliberately losing) → "拖后腿" (holding the team back) is softer; the literal "送人头" was considered and rejected because it accuses the Wi-Fi of feeding, which reads as blame aimed through the Wi-Fi at the user. "拖后腿" keeps the team-game frame without the accusation.
6. **gamer / points.earned** — EN: "+{v} XP." → ZH: "+{v} 经验。" Chinese games write 经验 for XP in running text; "+{v} XP" would also be understood. Either is fine — pick whichever matches the UI's other XP usages.
7. **support / wifi.drops** — EN: "Your Wi-Fi has been unsteady. I saved what happened before each drop." → ZH: "你的 Wi-Fi 一直不太稳，每次掉线前的情况我都记下来了。" The English claims the buddy saved pre-drop context; the Chinese says the same, but if the app does not actually log pre-drop context this line over-promises. Flag for the app side, not the translation.
8. **support / all.good** — EN: "All fine. Nothing needs your attention." → ZH: "都正常，没什么需要你担心的。" First draft used "操心" (to worry about), which contains the character 操; reworded to 担心 to avoid any naive profanity-filter substring match. Meaning unchanged.
9. **bossy / speedrunner** — EN: "Queue respected. Last one first." → ZH: "按顺序来，上一个优先。" "Queue respected" has no snappy Chinese equivalent; this is a paraphrase that keeps the order-enforcing joke. Alternative, shorter: "排队，先做上一个。"
10. **stoic / speed.bloat** — EN: "Fast, but the delay rises {v} when full. Moderation, even for a line." → ZH: "速度快，可满载时延迟多 {v}，连网线也要节制。" "Even for a line" (a network line should practice moderation) is a very Marcus-style joke; "连网线也要节制" renders it, but it is the most interpretive line in the stoic file. Alternative, plainer: "速度快，可满载时延迟多 {v}，凡事都要节制。"
11. **stoic / game.timeup** — EN: "Time is up. It was never ours to keep." → ZH: "时间到了，它从来不属于我们。" Kept the aphorism; note it is the most philosophical line in the file and may read as slightly heavy for a quiz timer, but that is the persona.
12. **stoic / trade.closed** — EN: "The market sleeps until 9:30. So may you." → ZH: "市场睡到 9:30，你也可以睡了。" "So may you" (permission to sleep) → "你也可以睡了" keeps the gentle blessing; a more literal "愿你也是" would sound stilted.
