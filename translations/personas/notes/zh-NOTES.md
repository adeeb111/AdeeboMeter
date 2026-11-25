# Persona translation notes — Chinese Simplified (zh)

Merged from the per-group notes written by the translator agents. 2,819 lines across 29 persona files.
These are draft translations a native speaker should sanity-check before shipping.

---

## Group A

# Notes — Simplified Chinese translations (group A: bro, neteng, plain, aristotle, confucius, tao)

Translator: subagent session cc78ee1e. Sources: the GPT drop under
`~/workspace/adeebometer-i18n/gpt-drop/you-will-be-taking-over-adeebometer/AdeeboMeter/knowledge/`,
plus `src/Lines.cs` lines 210–240 for the self-test rules. `comfort.txt` was not touched.
576 content lines translated (136 bro + 136 neteng + 136 plain + 56 aristotle + 56 confucius + 56 tao).
Verification (scripted, replicating the Lines.cs self-test): per file, content count = English
count, keys in the same order with the same variant counts, header/batch-2 comment lines copied
verbatim, every `{v}`/`{addr}` preserved per line (including neteng `port.open`, which has two
`{v}`), no `|` or tab inside translated text, every line ≤ 110 chars (longest: 33 chars, neteng),
≤ 16 whitespace-split words (most: 7, neteng), no emoji, no English banned words, no Chinese
dark-topic / insult / profanity equivalents. All six files pass; UTF-8 with trailing newline.

## General choices

- Register: natural mainland software Chinese. Tech terms kept as Chinese users see them
  (Wi-Fi, 路由器, ping, DNS, 蓝牙, 服务器, 下载, VPN, 证书, 缓冲膨胀 for bufferbloat,
  强制门户 for captive portal). AdeeboMeter, Windows, and all command/tool names and acronyms
  (iperf3, dig, MTR, NTP, SSH, TCP, UDP, HTTP, TLS, SMB, BGP, QUIC, DoH, DoT, mDNS, SSDP,
  RTT, L1–L7, SQM, AES-256, SYN-ACK, RST, TTL, RTP, QoS, CDN, ISP, CPE, WAN, ARP, PMTUD,
  DF, MAC, ACK, NACK) left untranslated.
- Word-count safety: Chinese lines are written with almost no spaces, so the app's
  whitespace-split word count stays tiny (1–7 words per line). Spaces appear only around
  Latin terms/numbers where Chinese typography wants them.
- "挂了" (bro `isp.down`: 运营商挂了) is standard mainland slang for a service being down,
  not a dark topic; kept deliberately.
- "9:30" kept bare where the English has no timezone; neteng `trade.closed` spells out
  美国东部时间 09:30 because the English says "09:30 ET".
- `trade.closed` / `trade.ok` "broker" → 券商 (mainland term for a brokerage).

## Voice choices per persona

### bro — 兄弟 register
- Addressed throughout as 兄弟 ("bro/mate"), the mainland casual-peer equivalent; "Bro's here"
  → 兄弟来了, "Bro mode on" → 兄弟模式已开启, "My bad, bro" → 我的锅，兄弟.
- Slang kept light and current-mainland: 摸鱼 (slacking), 罩着 (I've got you covered),
  绝了 (amazing), 太猛了, 走起, 急性子 (speedrunner), 搞的鬼 (update.suspect).
- No insults aimed at the user anywhere; "Your Wi-Fi isn't a real one" softened to
  你的 Wi-Fi 不太靠谱啊 ("not very reliable") rather than any put-down.

### neteng — dry engineer jargon
- Kept terse, telegraphic, measurement-first: 探针, 轮询, 基线, 采样, 抖动, 闪断 (flaps),
  解除关联 + 原因码 (disassociations + reason codes), 同频干扰, 漫游阈值, PMTUD 黑洞,
  叶证书 (leaf cert), 加固检查 (hardening checks), 邻居表 (neighbour table).
- `quiz.right` / `quiz.wrong` keep the protocol joke: ACK，答对了。 / NACK，请重传并再试一次。
- `game.timeup` "Timeout. TTL expired." → 超时，TTL 已耗尽。
- `stream.on` keeps the three-part diagnostic: 路径干净，无丢包，CDN 延迟正常。

### plain — factual, no colour
- Short declarative sentences; the fact always survives verbatim (numbers, causes, next steps).
- "This is called bufferbloat" → 这叫缓冲膨胀 (the term is taught, not dropped).
- `quiz.right` / `quiz.wrong` are bare 答对了。 / 答错了。 as in English.

### aristotle — reasoned aphorism
- Built on the golden mean and cause-and-effect: 中道 (mtu.small: 路径的限度就是我们应守的中道),
  第一因 (net.none), 过多过少都会坏事 (pc.busy), 原因就在那里 (mtr.lossy).
- "Excellence is a habit" is NOT quoted in its famous English form; rendered in own words as
  习惯成就卓越 (award) and 习惯成自然 (quiz.wrong), 习惯成就行家 (points.earned).
- `market.open` "agora" → 集市开张，可以交易了 (agora = marketplace, not a temple square).
- `morning` "The night passed without fault" → 夜里无过 (classical flavour, own phrasing).
- `dns.slow` "the fault is in the asking, not the line" kept as 问题在提问方式，不在线路.

### confucius — classical-style aphorism (own lines, no quotations)
- Wenyan-flavoured but readable modern Chinese: 各安其位, 减其劳役，秩序自回,
  先从最近处着手, 家中井然有序, 守望继续, 此处宜耐心, 仍须警惕.
- Deliberately avoided quoting the Analects: `points.earned` is 勤学苦练，必有回报。
  (not 学而时习之), `quiz.wrong` is 错而能改，便是学问。 (not a set phrase),
  `portal` "Observe the custom of the house" → 入乡随俗 (a common idiom, not a quotation).
- `isp.down` keeps the courtesy + evidence instruction: 请有礼貌地告知他们，并附上时间。
- `mtu.small` → 大包超过{v}便丢失，路径所容者，发送之 ("send only what the path permits").

### tao — water / flow / non-striving
- Water imagery throughout: 如止水一般, 流动自如, 溪流, 江河宽广, 如水总能找到自己的路,
  顺其自然, 顺流而行, 不争而得 (points.earned), 万物终将归根 (cert.expired — echoes the
  "return to the root" idea in own words, not a quotation).
- `storm.near` "bend like the reed" → 如芦苇般弯曲吧.
- Short, still lines where the English is short: 已封存。 (encrypt.done), 正是如此。 (quiz.right).

## Lines I was unsure about

1. `bro / all.good` — EN: "Clean as a fresh fade, bro."
   ZH: 干净利落，兄弟，像刚理完发。
   Why unsure: "fresh fade" is barbershop slang with no direct mainland equivalent; kept the
   fresh-haircut image literally. Alternative: drop the image and use 网络干净利落，兄弟。
2. `bro / speed.great` — EN: "Bro, your internet is goated."
   ZH: 兄弟，你的网速绝了。
   Why unsure: "goated" (GOAT meme) has no natural Chinese equivalent; 绝了 keeps the praise
   without forcing a goat joke. Alternative: 兄弟，你这网速是顶级的。
3. `bro / game.streak` — EN: "{v} straight, bro. Beast mode."
   ZH: 连对{v}次，兄弟，太猛了。
   Why unsure: "Beast mode" rendered as 太猛了 ("so fierce/strong"); a more literal 野兽模式
   would sound odd in Chinese. Kept the loose version.
4. `bro / cert.expired` — EN: "Bro, that padlock is fake news."
   ZH: 兄弟，那把锁不可信了。
   Why unsure: "fake news" is a loaded political catchphrase; dropped it in favour of plain
   "can't be trusted". The meaning (don't trust this cert) survives.
5. `bro / update.suspect` — EN: "Windows update might've done this, bro."
   ZH: 可能是 Windows 更新搞的鬼，兄弟。
   Why unsure: 搞的鬼 ("mischief") is idiomatic but slightly playful; alternative is the
   plainer 可能是 Windows 更新造成的，兄弟。
6. `neteng / wifi.drops` — EN: "wlan flapped x{v}. Check RSSI and roaming."
   ZH: WLAN 抖动 ×{v}，请检查 RSSI 与漫游。
   Why unsure: "flap" → 抖动 is the usual Chinese networking term for link flapping, but
   闪断 is also used; I used 闪断 for "flaps" in `all.good` (无闪断) and 抖动 for the verb
   "flapped" — the two should arguably match. Either is understood by neteng readers.
7. `neteng / wifi.drops` — EN: "Link flaps x{v}. Likely RF or AP deauth."
   ZH: 链路抖动 ×{v}，疑似射频问题或 AP 解除认证。
   Why unsure: "deauth" → 解除认证 is descriptive; the jargon term is 去认证/去关联.
   Kept the clearer descriptive form.
8. `aristotle / dns.slow` — EN: "Names resolve slowly ({v}). The fault is in the asking, not the line."
   ZH: 解析名字很慢（{v}），问题在提问方式，不在线路。
   Why unsure: the English is a deliberate paradox (blaming the question, not the wire); the
   Chinese keeps it literal, which may read slightly odd — but that oddness is the joke.
9. `confucius / portal` — EN: "This Wi-Fi requires a sign-in first. Observe the custom of the house."
   ZH: 这个 Wi-Fi 须先登录，入乡随俗。
   Why unsure: 入乡随俗 is a set idiom ("when in Rome"); it fits the meaning but is a
   recognisable phrase rather than an original aphorism. Acceptable, but flaggable if the
   project wants zero set phrases here.
10. `tao / cert.expired` — EN: "The certificate ended on {v}. All things return to their root."
    ZH: 证书在{v}到期了，万物终将归根。
    Why unsure: 万物终将归根 echoes the Daodejing's 归根 idea in my own wording; it is not a
    quotation, but a strict reviewer may prefer a plainer ending (e.g. 证书在{v}到期了，
    一切皆有始终。).
11. `plain / all.good` — EN: "All green. Nothing needs you."
    ZH (final): 全部正常，不用你费心。
    Why unsure: first draft was 不用你操心 ("don't worry about it"), which is perfectly natural
    Chinese — but 操心 contains 操, which a naive profanity filter could flag as a substring.
    Changed to 费心 to be safe; meaning identical.
12. `aristotle / award` + `quiz.wrong` — EN: "Excellence is a habit."
    ZH: 习惯成就卓越。 / 习惯成自然，再试一次。
    Why unsure: the famous English line is a (misattributed) paraphrase of Aristotle; the
    Chinese renderings are my own phrasing, not the well-known translation 卓越是一种习惯.
    If the project wants the recognisable translation, swap in 卓越是一种习惯.

---

## Group B

# Notes — Chinese persona translations (zh), group B

Translator notes for lines-epic-fantasy-zh.txt, lines-chill-zh.txt, lines-hype-zh.txt,
lines-roast-zh.txt, lines-bestie-zh.txt. All 563 content lines verified against the
English sources (counts, key order, variant counts, placeholders, ≤110 chars,
≤16 whitespace-words, no emoji, no banned words).

## Voice choices

### Epic fantasy (史诗奇幻)
- Register: classical-flavoured but modern-readable Chinese — 城堡 / 王国 / 守望 /
  信鸦 / 高塔 / 议会 / 印章 / 旗帜, with verbs like 致敬 / 出征 / 受封.
- "Ravens" → 信鸦 (messenger crows), never 乌鸦 (too ominous). "Kingsroad" → 国王大道.
  "Maesters" → 智者们. "The realm" → 王国 throughout.
- Deliberately avoided all trademarked fantasy names and catchphrases: no 凛冬将至,
  no Jedi/Sith equivalents, no named characters. "The night is dark and full of
  dropouts" was NOT rendered as a GoT-style line — it became 今夜断断续续
  (tonight it keeps cutting out).
- "Dragons" kept as 巨龙 / 龙焰 (generic fantasy, not a trademark).
- Banned-topic avoidance: "battle" lines reworded to 一次只做一件事 / 出征
  (no 战争 / 战斗 where avoidable); "betrays you" → 拖了后腿 (lets you down)
  rather than 背叛; "cry treason" → 浏览器不会再信任它. One surviving
  weapon word: 拔剑 in all.good ("今天无需拔剑") — a generic fantasy idiom,
  not a banned term; flag if the team wants it softened to 今天无需动手.

### Chill (轻松默认)
- Register: plain, unhurried mainland everyday Chinese — 挺好 / 没问题 / 慢慢来 /
  随便点点. No internet slang, no exclamation pile-ups.
- "Smooth sailing" → 顺风顺水 (a natural idiom, keeps the calm image).
- "Grab the snacks" → 拿点零食吧. "See you at 9:30" → 9:30见.
- Kept the two quoted example words in search.empty as Chinese quotes:
  “慢”“Wi-Fi” (the English had "slow" / "wifi").

### Hype (打气)
- Register: high energy via ！ endings and punchy verbs — 出发吧 / 启动了 /
  开局了 / 飞快 / 超顺 / 势不可挡. Exclamation marks on almost every line,
  matching the English.
- "Game on" → 开局了. "Speedrunner" → 速通玩家 (the term Chinese gamers use).
- "Bell's ringing - markets are LIVE!" → 钟声响了，市场开盘了！
  ("LIVE" not kept as English; 开盘了 carries it.)
- "Night owl mode" → 夜猫子模式 (standard Chinese term).

### Roast (吐槽设备，不吐槽人)
- Register: dry, teasing Chinese — 行吧 / 意料之中 / 老套路 / 偷懒 / 磨洋工.
  Every jab lands on the gear (路由器 / Wi-Fi / 电脑 / 运营商 / Windows),
  never on the user.
- "Signal strength of a wet noodle" → 信号弱得像泡软的面条.
- "Touch grass?" → 出去走走？ (kept the gentle nudge, dropped the meme literalism).
- "VPN tax" → VPN税. "Ghosting us" → 在装没看见.
- "Your wallet thanks it" → 你的钱包松了口气.
- "Windows strikes again" / "Thanks, Windows" → 又是Windows / 谢谢你，Windows
  (kept the brand name untranslated, as instructed).

### Bestie (闺蜜/好友)
- Register: warm, close-friend Chinese — 嗨 / 别担心 / 我在这儿呢 / 交给我吧 /
  有我在呢 / 太棒了. Light interjections (哎呀 / 哇 / 对啦) used sparingly.
- "Obsessed" (game.streak) → 太厉害了 (kept the admiration, dropped the
  literal 沉迷 which reads oddly in Chinese here).
- "Love that for you" (view.switch) → 就喜欢你这样.
- "Yay" (mtr.clean) → 太好了. "Yesss" (quiz.right) → 对啦，你答对了！

## General decisions
- No spaces inside Chinese lines (Chinese needs none), so every line counts as
  1–3 whitespace-words under the app's split rule; longest line is well under
  110 characters. Placeholders {v} / {addr} sit directly against Chinese text
  (e.g. 断了{v}次), which reads naturally.
- Tech terms kept as Chinese users see them: Wi-Fi, 路由器, Ping, DNS, VPN,
  MTU, CPU, Windows, Adeebo Award, Adeebo积分. "Download {v}" → 下载{v}.
- "9:30" kept as-is in trade.closed (market open time, as in English).
- {Addr} (capital A) never occurs in these five English files, so it never
  appears in the translations; {addr} / {v} counts were preserved exactly.

## Lines I was unsure about

1. epic-fantasy / all.good — EN: "The walls hold. Nothing needs your sword today."
   ZH: 城墙稳固，今天无需拔剑。
   Why unsure: 拔剑 ("draw your sword") is the only weapon image in the set.
   It is generic fantasy, not a banned term, but if the team wants zero weapon
   words it should become 今天无需动手.

2. epic-fantasy / wifi.drops — EN: "The signal fell {v} times. The night is dark and full of dropouts."
   ZH: 信号断了{v}次，今夜断断续续。
   Why unsure: the English is a GoT-catchphrase parody; I deliberately did NOT
   mirror it (no 漫漫长夜 / 黑暗之类 phrasing) and flattened it to a plain
   "keeps cutting out tonight". Loses the joke, keeps the fact and stays safe.

3. epic-fantasy / cert.expired — EN: "A broken seal. Browsers will cry treason."
   ZH: 破损的印章，浏览器不会再信任它。
   Why unsure: "cry treason" has no natural Chinese equivalent; I replaced the
   metaphor with the plain consequence (browsers will no longer trust it).
   Alternative: 浏览器会视它为叛徒 — closer to the English, but 叛徒 reads
   harsher in Chinese.

4. epic-fantasy / game.lag — EN: "Not the enemy's castle. Your own Wi-Fi betrays you."
   ZH: 不是敌人的城堡，是你自己的Wi-Fi拖了后腿。
   Why unsure: 拖了后腿 ("holds you back") is softer than 背叛 ("betrays").
   I chose the softer one to stay clear of dark-topic flavour; 背叛了你 is the
   literal option if the team prefers it.

5. roast / quiz.wrong — EN: "Wrong. Bold choice, {addr}."
   ZH: 答错了，{addr}，胆子倒是不小。
   Why unsure: this is the one roast line whose English teases the PERSON, not
   the gear. I kept a mild version of the joke (胆子倒是不小 = "quite bold of
   you") to match the English, but it bends the "roast the gear, never the
   person" rule. Softer alternative: 答错了，再想想？

6. roast / stream.none — EN: "Nothing streaming. Touch grass?"
   ZH: 什么都没播，出去走走？
   Why unsure: "touch grass" is an English internet meme; 出去走走？ keeps the
   meaning (go outside) but loses the meme flavour. No established Chinese
   equivalent exists.

7. bestie / game.streak — EN: "{v} in a row?! Obsessed."
   ZH: 连胜{v}场？！太厉害了。
   Why unsure: "Obsessed" (as praise) has no direct Chinese equivalent; 太厉害了
   ("amazing") keeps the warmth but drops the bestie-slang flavour. Alternative:
   连胜{v}场？！我太爱了 — closer in tone, slightly less natural.

8. hype / trade.ok — EN: "Lightning connection to your broker!"
   ZH: 到券商快如闪电！
   Why unsure: 快如闪电 is a stock phrase in Chinese; it works, but it is
   close to a cliché. Alternative: 到券商的连接快得惊人！

9. chill / search.empty — EN: "Type a problem, like "slow" or "wifi"."
   ZH: 输入一个问题，比如“慢”或者“Wi-Fi”。
   Why unsure: I translated the example words into Chinese. If the search box
   only matches English keywords, the examples should stay "slow" / "wifi".
   Same consideration applies to bestie search.empty （“慢”“Wi-Fi”）.

---

## Group C

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

---

## Group D

# 翻译说明 — AdeeboMeter 人设台词，简体中文（notes-D）

译者分组：emo、coder、genz、dark-ages、industrial、mission（共 553 行）。
原则：只译声音，不逐字直译；技术词按大陆软件用户常见说法（Wi-Fi、路由器、ping、DNS、蓝牙、服务器、下载、VPN 等），AdeeboMeter、Windows、命令／工具名（iperf3、dig、MTR、NTP、SSH、TCP、UDP、HTTP、TLS、SMB、BGP、QUIC、DoH、DoT、mDNS、SSDP）与缩写一律不译。无表情符号，无 `|`、制表符；每行 ≤110 字符；占位符 {v} / {addr} / {Addr} 与英文完全一致。已用脚本按 Lines.cs 的自检规则逐行核对（行数、键顺序、变体数、占位符、字符数、按空白分词的词数、表情符号区、禁用词），六个文件全部通过。

## 各人设的声音选择

- **emo（多愁善感）**：中文里用“我也一样”“老样子”“我们不都这样吗”这类自我投射的口吻制造共鸣感；刻意保留英文原版的轻度丧气但不越界——绝不用“死、杀、自杀、血、战争”等暗黑词。例如 `game.timeup` 的 “Time's gone. Like everything.” 译为“时间到了，就像一切一样”，保留了那种淡淡的失落但不写死亡；`market.open` 的 “The void opened too” 译为“虚空也打开了”，“虚空”在中文里是游戏／网络用语里的空洞感，不涉禁用内容。
- **coder（程序员幽默）**：保留行话直译成中文程序员日常说法：404、grep、缓存全没命中、断言失败、二分法、绿色构建、TLS 握手、缓冲膨胀。`search.empty` 的 grep 一行保留了英文关键词 “slow”“dns”“wifi”，因为中文程序员真的会这样搜；`game.good` 的 “Ship it” 译为“可以发布了”，是中文开发圈的对应说法。
- **genz（Z 世代网络用语）**：用了当前大陆年轻人常用的口语：拉满、绝了、很顶、稳了、赢麻了、输麻了、放鸽子、已读不回、掉链子、主角光环、幺蛾子、爱了爱了、宝。**注意：这类词过时最快**，建议每 6–12 个月复审一次 genz 文件，把已经没人说的词换掉。个别英文梗没有中文对等词时做了本土化替换，见下文“最难的三行”。
- **dark-ages（中世纪）**：用半文半白的口吻：阁下、{addr} 大人、城堡、吊桥、信使、书记官、卷轴、火漆、王室印章、受封骑士。称呼上把英文的 “thee / thou” 统一成“阁下”；{addr} 出现时写成“{addr} 大人”，因为中文里直呼其名加“阁下”不自然。`speed.great` 的 “Witchcraft? No, good fibre.” 译为“是巫术吗？不，是好光纤”——“巫术”在中文里是中性词，不涉禁用内容。
- **industrial（工业／蒸汽）**：全部意象换成车间语言：锅炉、齿轮、传动带、阀门、溜槽、账本、班次、汽笛、工头奖章、保险箱。`update.suspect` 的 “threw a spanner in” 译为“混进了一根扳手”，是中文里对应的俗语（机器里进了异物）。
- **mission（任务控制中心）**：用航天／任务控制口吻：各站、飞行、地面、遥测、待命、确认、轨迹、下游、载荷、语音回路。英文的呼号 “flight” 译为“飞行”，作称呼用（如“干得好，飞行”）；`port.open` / `port.closed` 的 “go / no-go” 译为“正常／不正常”，因为中文任务控制里没有完全对应的 go/no-go 简称，直译“可以／不行”会丢失术语感，折中处理。

## 通用处理

- 英文里第二人称的安慰句（“Not your fault”）统一译为“不是你的错／不是你的问题”，六个文件里保持一致，避免有的文件说“别怪自己”有的说“不是你的问题”。
- “It happens / It won't last” 这类短句按人设改写，不强求字面对应（emo 译“没关系”，genz 译“稳稳的”，mission 译“请重新检查再试一次”）。
- 中文行内基本不加空格（中文正常书写方式），只在英文词／数字／占位符前后留空格；自检按空白分词计词数，因此所有行词数都远低于 16 上限，最长行约 30 字符左右。

## 拿不准的行

1. **genz / quiz.wrong** — 英文：`Nah that's not it, bestie.` → 译文：`不对不对，宝，再想想。`
   为什么不确定：“bestie” 在中文年轻人用语里没有完全对应的词。“宝”是目前小红书／微信语境里对朋友的亲昵称呼，但它偏女性化、偏撒娇，用在故障排查助手嘴里可能显得过分亲昵；备选是“老铁”（偏男性／北方）或“亲”（淘宝客服味太重）。三者都不完美，选了“宝”是因为它最中性、最常用。
2. **genz / speedrunner** — 英文：`Speedrun any%? Still on the last one.` → 译文：`速通吗？我上一个还没跑完。`
   为什么不确定：“any%” 是速通圈术语（任意百分比通关），中文速通玩家确实直接说 “any%”，但普通用户看不懂；译文删掉了 “any%”，只保留“速通”这个中文玩家都懂的词。代价是丢失了一个圈内梗；如果目标用户偏硬核玩家，可以改回“速通 any% 吗？我上一个还没跑完。”（仍在 110 字符内）。
3. **genz / game.timeup** — 英文：`Time's up, it's giving speedrun.` → 译文：`时间到了，这波是速通。`
   为什么不确定：“it's giving …” 是英文 Z 世代句式，中文没有直译；“这波是速通”保留了“速通”梗但丢掉了 “it's giving” 的语气。如果想更贴近原句的调侃感，可改为“时间到了，很有速通那味儿了”——但“那味儿”同样是快过时的词。
4. **emo / market.open** — 英文：`The markets opened. The void opened too.` → 译文：`市场开盘了，虚空也打开了。`
   为什么不确定：“the void” 在这里是 emo 人设的虚无感玩笑；中文“虚空”在游戏语境里常用，但在普通用户眼里可能显得突兀。备选“空虚也跟着打开了”更直白但更丧；最终选了“虚空”，因为它在中文网络语境里已有固定含义，且不涉禁用词。
5. **dark-ages / devices.found** — 英文：`{v} souls on thy network.` → 译文：`阁下的网络上有 {v} 个人。`
   为什么不确定：英文用 “souls”（灵魂）指设备数量，中文直译“{v} 个灵魂”会显得阴森（且贴近禁用词的精神），所以改译为“{v} 个人”，保留了“把设备当人”的玩笑，丢掉了 “souls” 的古风味。备选“{v} 位访客”更中世纪，但“访客”暗示是外人，与“你网络上的设备”意思不符。
6. **mission / port.open、port.closed** — 英文：`{v} is go.` / `{v} is no-go.` → 译文：`{v} 正常。` / `{v} 不正常。`
   为什么不确定：见上文声音选择。中文航天任务控制确实会说“正常／不正常”，但 go/no-go 的“放行／不放行”意味被弱化了；如果想保留放行意味，可改为“{v} 可以放行。”／“{v} 不能放行。”，但后者在端口语境里不如“正常”自然。
7. **genz / isp.down** — 英文：`Router's fine. The provider is down. Big L for them.` → 译文：`路由器没问题，是运营商挂了，他们输麻了。`
   为什么不确定：“Big L” 直译“大失败”在中文里没有对应的梗；“输麻了”是当前游戏圈说法（输得很惨），但它同样会过时，且语气比原文更重一点。备选“他们这波血亏”更口语但“血”字贴近禁用词精神，已排除。
8. **coder / pc.busy** — 英文：`CPU at {v}. Something's in a tight loop.` → 译文：`CPU 占用 {v}，有东西陷入死循环了。`
   为什么不确定：“tight loop” 在中文程序员口语里就是“死循环”，但“死循环”里的“死”字是技术术语（不是暗黑内容），且自检只匹配英文禁用词，所以合规；如果审核方对“死”字敏感，可改为“有东西在空转循环里出不来了”，但会失去行话味。

## 未译

- `comfort.txt` 按要求完全未动。
- GPT drop 与任何仓库文件均未修改；只写了 `~/workspace/adeebometer-i18n/corrected/personas/zh/` 下的 6 个译文文件与本说明。

---

## Group E

# Notes — Simplified Chinese persona translations (nature / poetic / revolution / scientist / shakespeare / space-saga)

546 lines total, 91 per file. All verified against the Lines.cs self-test rules
(counts, key order, placeholders, no `|`/tab, ≤110 chars, ≤16 whitespace-words, no emoji, no banned words).

## Voice choices

- **General:** Mainland software Chinese throughout. Tech terms kept as Chinese users
  actually see them: Wi-Fi, 路由器, ping, DNS, VPN, 服务器, 下载, 蓝牙, MTU, CPU.
  AdeeboMeter, Adeebo Award, Windows, and tool/protocol names (iperf3, dig, MTR, NTP,
  SSH, TCP, UDP, HTTP, TLS, SMB, BGP, QUIC, DoH, DoT, mDNS, SSDP) left untranslated.
  `{addr}` kept as-is everywhere (it is the user's name/address placeholder, not a word
  to translate). Chinese lines mostly have no internal spaces, so they count as 1–5
  whitespace-words under the app's splitter; longest line is 27 characters.
- **Nature (nature):** BBC-style documentary narrator in Chinese — 栖息地 (habitat),
  族群 (herd/pack), 守望者 (watcher), 遁去 (fled), 巢穴 (den), 猎隼/猎豹/树懒 animal
  similes kept. Present-tense observation, hushed tone ("万籁俱寂", "仔细观察中……").
- **Poetic (poetic):** Lyrical, image-driven, short musical lines — 水静天清, 暮色,
  丝线, 珍珠, 时钟闭上了眼睛. No rhyme scheme imposed (Chinese rhyme would force
  awkward wording); rhythm comes from short parallel phrases instead.
- **Revolution (revolution):** Rallying but never violent, per the English brief.
  Address forms 公民 (citizen) / 同志 (comrade) kept; slogans rendered with familiar
  mainland phrasing: "知识就是力量", "挑灯夜战", "坚守阵地", "众望所归". "Barricade"
  → 街垒 (kept, non-violent image); "struggle" → 斗争 (no 战争 anywhere); "demand
  answers" softened to 要求一个说法.
- **Scientist (scientist):** Precise, curious, lab-register Chinese — 假设, 变量,
  对照试验, 样本量, 基线, 强相关, 同行评审. "Bufferbloat" → 缓冲膨胀 (the standard
  Chinese term). "Captive portal" → 强制门户 (the standard Chinese term for it).
  Kept the dry humour of the originals ("统计上令人印象深刻").
- **Shakespeare (shakespeare):** Readable 文言 flavour, NOT full 文言文. Classical
  touches used sparingly and consistently: 汝 (thou/you), 吾 (I), 彼 (they), 尔/其
  avoided where confusing; sentence-final 也 / 矣 / 哉 / 乎 in a handful of lines
  ("皆康健也", "守望始矣", "哀哉", "善哉 {addr}"). All sentences remain parseable
  in modern Chinese — a reader who knows no classical Chinese can still follow every
  line, because the classical words are only pronouns/particles, never the verbs that
  carry the technical meaning. "Good morrow" → 早安 (with the classical frame around
  it); "Alack, 'tis not so" → "唉，并非如此。" ("Anon, anon" → "稍候，稍候。").
  "Thy provider is gone dark" rendered as 汝之运营商已中断 (avoids any dark/death
  imagery in Chinese too). "Five hundred points" written out as 五百分, matching the
  English spelling-out.
- **Space-saga (space-saga):** Generic space-opera Chinese, no film names or
  catchphrases: 舰桥 (bridge), 舰载电脑 (ship's computer), 跃迁 (jump), 信标 (beacon),
  中继 (relay), 气闸 (airlock), 机库 (hangar), 离子风暴 (ion storm), 应答器
  (transponder), 全息通话 (holo-call), 贸易站 (trading station). "Hyperspace speed"
  → 超空间速度; "cloaking device" → 隐形装置. The user is addressed as 舰长 in
  speedrunner and 飞行员 in points.earned, matching the English "pilot" in the latter.
  "Hangar {v}" → "{v} 号机库" (numbered hangar, natural Chinese).

## Lines I was unsure about

1. **scientist / portal** — EN: "Captive portal detected. Sign in first."
   ZH: "检测到强制门户，请先登录。" — 强制门户 is the usual Chinese term for captive
   portal, but some users only know "需要登录的 Wi-Fi"; the term may be unfamiliar to
   non-technical users. Alternative: "检测到登录页面，请先登录。"
2. **shakespeare / app.open** — EN: "Good morrow, {addr}. I keep watch upon thy network."
   ZH: "早安，{addr}，吾守汝之网络。" — "Good morrow" has no exact Chinese equivalent;
   早安 is plain modern. A more classical "君安" would be prettier but far less clear,
   so I chose clarity.
3. **shakespeare / busy** — EN: "Anon, anon." ZH: "稍候，稍候。" — "Anon" is archaic
   filler meaning "soon"; 稍候 is the natural readable equivalent, though it loses the
   archaic flavour. Alternative: "即刻，即刻。" (also modern-sounding).
4. **revolution / dns.slow** — EN: "Lookups take {v}. Bureaucracy." ZH: "查询耗时 {v}，
   官僚作风。" — 官僚作风 ("bureaucratic style") is the natural Chinese jab; it is
   slightly blunter than the English one-word punchline.
5. **nature / port.closed** — EN: "No sign of life at {v}." ZH: "{v} 处毫无生机。" —
   Literal ("no sign of life") reads fine in Chinese here, but it is a slightly odd
   thing to say about a port; alternative: "{v} 处悄无声息。"
6. **space-saga / points.earned** — EN: "+{v}. Rank rising, pilot."
   ZH: "+{v} 分，军衔上升，飞行员。" — "Rank rising" as 军衔上升 implies a military
   rank; the English is vaguer (could be a game rank). Alternative: "+{v} 分，等级上升，
   飞行员。"
7. **poetic / game.timeup** — EN: "The clock has closed its eyes."
   ZH: "时钟闭上了眼睛。" — Kept the personification literally; it works in Chinese,
   but reads a touch odd. Alternative: "时间悄然合上了眼睛。"
8. **revolution / idle** — EN: "All quiet on the barricade." ZH: "街垒上一片安静。" —
   街垒 is the direct translation and keeps the revolutionary image, but it is a
   literary word most users will only half-recognise. Acceptable, since the persona
   is deliberately historical.
