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
