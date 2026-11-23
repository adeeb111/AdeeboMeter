# AdeeboMeter — How to Use It

*Every section, what it's for and how to use it, in plain words. Version 1.1.5.*

---

## 1. The idea in one breath

AdeeboMeter answers one question: **is it me or the network?** It sits in the corner of your screen and watches your PC,
your Wi-Fi, your router, your provider and the internet. When something goes wrong it tells you, in one sentence, whose
fault it is and what to do. When you want more, one click opens everything behind that sentence.

```
   you glance ──▶  the corner widget   (always there, faded, never in the way)
   you click  ──▶  the engineer view   (everything: pages, tools, history, lessons)
   you press Esc ──▶ back to the corner (the view closes and gives its memory back)
```

---

## 2. First start

1. **Get it** from the Microsoft Store (easiest, signed, updates itself) or the website's download button.
2. **Open it.** A copy started from a new folder offers *Install*, *Just run it from here* or *Update*. No administrator
   rights are ever needed.
3. **Answer four quick questions** (what you mostly use the PC for, how the corner widget should look, how much to watch,
   how the buddy should talk). They pick a starting **view** - you can change everything later. *Skip* is always there.
4. The widget appears bottom-right. That's it - it now watches by itself.

---

## 3. The corner widget

```
 folded (most of the time)                hovered (rest the mouse on it)
 ┌──────────────────────────────────┐      ┌───────────────────────────────┐
 │ (•‿•) All quiet │ ↓ 12 ↑ 1 Mb/s │4°│      │ AdeeboMeter  SPEED        (•‿•)│
 └──────────────────────────────────┘      │ verdict · why · what to do     │
   buddy · status · speed · weather         │ ↓ download   ↑ upload  graph   │
                                            │ Today ↓ 1.2 GB  ↑ 90 MB        │
                                            │ 4°C · rain from 3 pm           │
                                            │ ● PC ● LAN ● ISP ● DNS ● Net   │
                                            └───────────────────────────────┘
```

- **Glance, don't click.** Green and quiet means fine. A colour and a short sentence means something needs you.
- **Hover** to open the card; **click** it (or the tray icon, or **Ctrl+Alt+A**) to open the engineer view.
- **The five dots** are the path from your PC to the internet. The first one that isn't green is where the trouble starts.
- **The buddy** shows how your network feels: smug when all is green, dizzy on bad Wi-Fi, betrayed when your provider
  lets you down. Click the face in the engineer view to change its mood, voice and words of comfort.
- **How it behaves** (Settings > Widget): *Quiet* never changes by itself; *Quiet + moments* shows a face for 12 seconds when
  something good happens (a clean game, a new speed record); *Follows my view* also shows what your view cares about.
- **Widget style**: everyday card, speed (DU-Meter style, with or without status), gamer, calls or engineer.

---

## 4. The engineer view - finding your way

```
 ┌ logo ─ [VIEW ▾] ─ [ Search… Ctrl K ] ─ ☀/☾ ─ (•‿•) ─ ⚙ ─ ⋯ ─ [Back to corner] ┐
 │ ● PC ● LAN ● Internet      the verdict, in one sentence, and the buddy's line  │
 ├────────────────────────────────────────────────────────────────────────────────┤
 │ Home · Outlook · Streaming · Markets · Toolbox · Time machine · Learn · More ▾  │
 │                                                                                │
 │                        the page                              │  side panel     │
 └────────────────────────────────────────────────────────────────────────────────┘
```

The bar shows the sections you keep there (these are the default, as a side bar or a tab row). *More ▾* is not a section -
it's the menu with the rest: **Gamer**, **Network story**, **Security**, **Check a destination**, **Calculators** and
**Reports**. Search and Home's *Where everything lives* reach them too.

| Control | What it does |
|---|---|
| **View** | switches the whole app to fit what you're doing (see 5) |
| **Search** (Ctrl+K) | jumps to any page, tool, setting, program or term - it forgives typos |
| **☀ / ☾** | light or dark (Settings has four more themes) |
| **The face** | the buddy sheet: mood, voice, fun lines, words of comfort |
| **⚙** | Settings |
| **⋯** | Details (raw values and sources), sidebar layout, Adeebo Points, About, report a bug |
| **Back to corner** (Esc) | closes the view; the widget keeps watching |

**Mouse and keyboard:** wherever the cursor becomes a **?**, hovering explains it (F1 too). **Right-click** anywhere for
zoom, copy and save a picture of the window. **Ctrl + / Ctrl -** (or Ctrl + wheel) zoom from 80% to 175%.

---

## 5. Views

A view decides which sections show, the widget style and how much is watched.

| View | Made for |
|---|---|
| **Everyday** | browsing, calls, "is it me or the internet?" |
| **Gamer** | ping, jitter and loss to your game's servers; the Gamer Zone |
| **Streamer** | Netflix, YouTube, Twitch - every stream's dips and whose fault they were |
| **Trader** | the line to your broker in market hours; currencies and commodities |
| **Engineer** | everything technical, all tools in front |
| **Everything** | every section at once |
| **Play + watch, Play + trade, Watch + trade** | two at once |

Click **View**, pick one - a short card says it's switching. Each view remembers your own changes.
*+ Make a view from what I have now* saves your own mix under a name; a built-in view can be put back as it ships.

---

## 6. Home

- **Today** - the verdict, router / internet / DNS / traffic right now, what needs attention, and **Your week**: the last
  seven days across everything you do (streams, trading, games, outages), whose fault each bad moment was, and the Learn
  topic that explains it.
- **What's wrong?** - press it when something feels off. It checks only what's stale and answers in one sentence with the
  evidence underneath.
- **Connections** - who your PC is talking to, by program, with names and countries.
- **Your Wi-Fi / cable** - signal, channel, speed, roams and drops, neighbours on your channel.
- **What's running** - every program in plain English: what it is, whether it's normal, whether it's busy.
- **Live capture** - (with Wireshark installed) watch traffic live; stops by itself.
- **Health** - a once-over of the PC: disk, memory, updates, drivers, startup apps.

---

## 7. Streaming

Turn on *Notice streams* and play something. AdeeboMeter recognises the service by its servers' names (never your tabs or
titles) and records each stream: how long, every dip, and whose fault it was - your Wi-Fi, your provider or the service.

```
   YouTube in Chrome · 24 min          ▁▂▇▇▇▇▇▁▇▇▇▇▇▇▇▇▇▇▇▇     2 dips · Wi-Fi 2
   Netflix in Edge   · 1 h 35          ▇▇▇▇▇▇▇▇▇▇▇▇▁▇▇▇▇▇▇▇     1 dip · provider 1
```

The heatmap shows when your streams dip across the month; *For your provider* sums it up for a call to support.

---

## 8. Trading

- **Your broker** - name the broker; AdeeboMeter knocks lightly on its servers while the market is open (holidays and half
  days known) and records every slow moment. *Crypto, around the clock* watches 24/7.
- **Day's numbers** - the Bank of Canada's daily currencies and weekly commodity index, with what moved.
- **Evening summary** (opt-in) - after the close the folded widget shows the day's close for an hour and a half.

Everything here is **numbers, never advice** - AdeeboMeter never says what to buy or sell.

---

## 9. Gamer Zone

Add your games. Each game's regions are pinged in the background; while a game is open, its servers are checked every
second. You get ping, jitter, loss and a grade, a match report when you close the game, and *who caused a spike* - your
Wi-Fi, your PC, your provider or the game's server.

---

## 10. Toolbox

232 tools in six sections - **Network**, **Devices**, **Traffic Lab**, **Engineer**, **Hardware nerds**, **Windows** - plus **Work it out** (the
calculators). Browse *By question* ("Why can't I reach this site?") or *All tools*.

```
   pick a tool ──▶ fill the boxes ──▶ Run ──▶ result rows + a picture
                    or press Example   (made-up data, clearly marked, nothing sent)
```

- **The picture** shows how the tool works before you run it, the Example's result after Example, and yours after Run.
- **Example** shows what a result looks like; click its banner to fill the boxes with the example's inputs.
- **Copy as text**, **Save**, and **Compare with the earlier run** sit above every result.
- **Pins** - pin the tools you use most; they show first.

### 10.1 Work it out - the calculators

```
   ┌ Download [ 300 ] (Mbps)(Gbps)   Upload [ 20 ] (Mbps)
   │ Busy at the same time:  4K video [–] 2 [+]   Video call [–] 1 [+]   Online game [–] 1 [+]
   └─▶ the answer and the picture update as you type - no Run button
```

One value per box, units as chips, counts with - and +, lists with + add. Groups: network maths (subnet, VLSM, packet
stack, BDP, bits and bytes), wireless (LTE/5G, Wi-Fi, link budget), servers and IT (RAID, backup, UPS, SLA, storage
growth), streaming and home (bandwidth, bitrate, data use, *is my plan fast enough?*), voice (jitter buffer, E1/T1),
**money and markets** (currency, profit and break-even, position size, average cost, compound growth, fall-and-recover,
trade latency), **commodities** (units, futures contracts, price per unit) and **games** (ticks behind).

---

## 11. Time machine

- **Replay** - drag back through any minute of the last days (History must be on). The whole app shows that minute.
- **What changed** - a timeline of every change: new Wi-Fi, new DNS, a program that started talking, an outage.

---

## 12. Learn

- **Topics** - two lines and a picture each: how the internet works, protocols, your provider, mobile networks, Windows,
  crypto, streaming and **Money & investing** (bull and bear markets, recession, inflation, compound growth, emergency fund,
  budgeting, fees). Each has one *check yourself* question.
- **Labs** - play with TCP, bufferbloat, Wi-Fi airtime, NAT, MTU black holes and more.
- **Games** - escape room, catch the thief, packet detective, the packet dungeon, survive the outage, *Break the Adeebo Award* (a heist),
  quick-fire rounds, the streaming and money rounds (*Buffer race*, *Whose fault?*, *4K or not?*, *Spot the scam*) and
  *Ten years of money*.
- **Inside Windows** - what each Windows service and background program does.

Learning earns **Adeebo Points** (⋯ > Adeebo Points) - rank up, unlock buddy hats, and claim an Adeebo Award for the Hall
of Fame.

---

## 13. More

- **Network story** - follow one connection box by box, from the app to the server, with the labels each hop adds.
- **Security** - a 42-check checkup of your PC and network, in plain words, with what to do.
- **Check a destination** - is this site, server or game reachable, and how well? Pin it to watch it all day.
- **Report for my ISP** (Toolbox > Speed) - evidence your provider can't argue with.

---

## 14. Settings

Open with **⚙**. Settings start as questions ("How should the corner widget look?"); *Fine-tune* shows every switch. Each
setting says what it touches - *on this PC*, *your network*, *the internet* or *online lookup* - and **Right now this PC
sends** sums up everything that leaves your PC.

**Weather** (Settings > Weather, off by default): type a city or a country, pick the match, and the buddy shows hot, cold,
rain, snow or storm for 2-3 minutes a few times an hour; the widget can show the temperature. Normal accuracy asks about
every 2 days, Better every 6 hours. Your PC's location is never used.

---

## 15. Telling Adeeb something

⋯ > **Report a bug**, or the website's feedback form. You see exactly what is sent - user and PC names, folders and
addresses are removed - and nobody needs an account. After a crash, the balloon offers the same box with the crash note.

---

## 16. What stays private

Everything stays on your PC unless you start it. Nothing is sent in the background except the small checks you can see and
switch off in Settings, all listed in *About > What leaves your PC*. No cloud, no account, no telemetry, never listens.

---

## 17. Quick answers

| You want to... | Do this |
|---|---|
| know if it's you or the internet | glance at the widget; the first non-green dot is where it starts |
| prove a slow evening to your provider | Home > Today > Your week, then Toolbox > Report for my ISP |
| find any tool | Ctrl+K and type a word |
| make text bigger | Ctrl + (or right-click > Zoom) |
| stop the corner from changing | Settings > Widget > How it behaves > Quiet |
| see what a tool does before using it | open it and press Example |
| learn the basics | Learn > Start here |

---

*Created by Adeeb Khan. AdeeboMeter — is it me or the network?*
