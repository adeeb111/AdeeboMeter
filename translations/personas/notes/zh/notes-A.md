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
