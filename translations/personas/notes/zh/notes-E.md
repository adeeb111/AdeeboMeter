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
