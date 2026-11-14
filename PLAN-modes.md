# AdeeboMeter - Modes (Streaming, Trading) on one Watch engine

Status: design agreed in chat 2026-11-14; nothing built yet. Ships inside 1.1.5 (one release).

## 1. Watch engine (shared)

```
            ┌── Gamer      targets: game servers              (existing - moves onto the engine)
            ├── Calls      targets: Teams / Zoom / WhatsApp   (existing widget - moves onto the engine)
 Watch  ────┼── Streaming  targets: video servers, auto-detected from app/browser connections
 engine     └── Trading    targets: the user's broker / exchange
 shared: probes · dip detection · whose-fault evidence · 90-day records · alerts · widget slot · usage budget
```

- One `Watch` per target: probe interval, active window (always / while app streams / market hours), budget.
- Dip = a target slower than its own normal or losing replies. Each dip stores evidence:
  router delay, internet delay, target delay, Wi-Fi signal, loss, busiest PC app at the time.
- Verdict per dip: This PC / Your Wi-Fi / Your provider / The far end (server, broker) / Not sure - same rules for every mode.
- Records: one line + dips per session, ~2 KB, kept 90 days, one shared store.
- Budget: the engine refuses intervals under 2 s; 2 s only for Trading during market hours (opt-in).

## 2. Streaming

- Detects the stream by the service's server names - apps and browsers alike; never reads tabs or titles.
  YouTube, Twitch, Netflix, Prime Video, Hulu, Disney+, Max, Apple TV+, Paramount+, Peacock, Crave, DAZN, Tubi,
  Crunchyroll, Hotstar, IPTV players (TiviMate, VLC, Kodi). Manual "I'm watching..." for anything else.
- Default page: Now streaming + Today in sport (newspaper) + 90-day history.
- Teams (optional): fixtures + scores with the user's free key (cricketdata.org, football-data.org, balldontlie NBA);
  every 5 min only while a followed match is live; no key -> "Live scores aren't shown here without a score key."
- Record page: whose-fault tally, timeline, each dip with evidence, Copy for my provider.
- Extras: 4K check, busy-hours grid, who else was using the connection (this PC only), monthly report.
- Widget: never changes by itself; "Speed + score" only if picked in Settings > Widget.

## 3. Trading

- Active trader (top): live broker panel, check every 2 s in market hours, alert >100 ms or lost reply, whose fault live.
- Casual investor (below): after-the-close 30-second read, your list (daily), market movers by country
  (filters out <$1, warrants, tiny caps), oil/gas/materials (weekly), currencies (daily), coming up (earnings, holidays).
- Currencies: Bank of Canada daily rates (keyless, official): USD, EUR, GBP, CNY, JPY, INR, CHF, AUD, ... vs CAD.
  BDT is not in that list - needs another source or is left out.
- Widget: unchanged; opt-in "Evening market summary" 17:30-19:00 and opt-in live "Broker 28 ms" for active traders.
- Never: logs in to a broker, sees an account, suggests buying or selling.

## 4. Learn additions

Streaming topic group:
- Lessons: how a video stream works (buffer + adaptive quality), what a CDN is, why 4K needs a steady line,
  Wi-Fi distance and walls, IPTV vs normal streaming, reading your own stream record.
- Games: Buffer race (keep the buffer from emptying by picking quality), Whose fault? (read the evidence, blame
  Wi-Fi / provider / server), 4K or not? quick-fire.

Investing basics topic group (education, never advice):
- Lessons: stock, index, ETF; why prices move; end-of-day vs live vs delayed data; percent change; currency effect on
  a foreign stock; fees; why spreading money out matters; what "most active" means; why your connection matters to a
  trader (and what it can't tell you).
- Games: Spot the scam (pump-and-dump, "guaranteed" returns, fake screenshots), The penny-stock trap (why +300%
  lists are risky), Delayed or live?, Currency trip (a US stock bought in CAD), Lag lab (same order with 30 ms vs
  300 ms to the broker - what changes and what doesn't).

## 5. Still to confirm before building the data parts

- Terms: Alpha Vantage (stocks, commodities, movers), CoinGecko key, a sports-headlines feed, FMP for Canada movers,
  earnings-date source, Bank of Canada terms page.
- Without a confirmed source the matching section is left out, never scraped.

## 6. Also open for 1.1.5

- Real-PC draw-time check (Inter body text, 30 ms budget) - test-1.1.5c on Adeeb's PC.
- adeebometer.com link switch (waiting for go).
- Unknown publisher: Adeeb is handling code signing.
