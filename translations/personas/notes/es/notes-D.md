# Spanish persona translations — notes (Group D: emo, coder, genz, dark-ages, industrial, mission)

Translator: subagent session a3432206. Sources: the GPT drop under
`~/workspace/adeebometer-i18n/gpt-drop/you-will-be-taking-over-adeebometer/AdeeboMeter/knowledge/`
(`lines-emo.txt`, `lines-coder.txt`, `lines-genz.txt`, `lines-dark-ages.txt`,
`lines-industrial.txt`, `lines-mission.txt`) plus the self-test rules in
`src/Lines.cs` lines 210–240. `comfort.txt` was not touched.
All six files verified mechanically against the English originals: 553 content
lines, key order and variant counts identical, every `{v}`/`{addr}`/`{Addr}`
preserved, no `|` or tab inside text, every line ≤16 words and ≤110 chars,
no emoji, no match for the self-test banned-word regex, UTF-8 with trailing
newline, header comment copied verbatim.

## General choices

- Neutral pan-Hispanic Spanish throughout: no vosotros, no region-only slang.
  (One deliberate exception is discussed under dark-ages: "¡Escuchad!" — see below.)
- Tech terms kept as Spanish speakers actually say them: Wi-Fi, router, ping,
  DNS, VPN, MTU, CPU, PC, Bluetooth-style acronyms, and all tool/protocol
  names (HTTP, TLS, TCP, UDP, SSH, NTP, iperf3, dig, MTR, QUIC, DoH, DoT,
  mDNS, SSDP, SMB, BGP) left untranslated. "Download" → "Descarga"/"de bajada";
  "upload" never appears. "Server" → "servidor". "Build" kept as "build" in
  coder (Spanish devs say "build en verde"); "commit", "grep", "changelog",
  "bufferbloat", "lint", "jitter", "hash", "gateway" kept or lightly adapted
  ("gateway" → "gateway" in coder's timeout line; everywhere else "router").
- "Adeebo Award" → "Premio Adeebo" in every persona (a name, translated
  descriptively the same way in all six files for consistency).
- "hop" → "salto" everywhere (MTR/traceroute usage; "salto" is the standard
  Spanish term).
- "PC" kept as "PC" (not "ordenador"/"computadora", except mission's
  "Computadora de a bordo" for "onboard computer", which is the NASA-style
  phrasing).
- No line uses gendered self-reference for the buddy (avoided "despierta/o",
  "segura/o", etc.) so the voice stays gender-neutral.

## Voice choices per persona

### emo — melodramatic, moody, soft
- Sighing, self-aware, quietly dramatic: "Se siente raro, la verdad",
  "Es la señal, no tú", "Todo tiene un costo", "Nada dura".
- "Same, PC. Same." → "Igual que yo, PC. Igual." (kept the stutter-repeat).
- "Patience is a vibe." → "La paciencia es un estado." — "vibe" has no good
  emo-Spanish equivalent here; "un estado" keeps the resigned shrug.
- "The void opened too." (market.open) → "El vacío también." Kept the
  melodrama but stayed light, per the header ("never dark") — no death/dark
  vocabulary anywhere in this file.
- "Wrong. Like everything else." → "Mal. Como todo lo demás." — blunt and
  bleak in the emo register, but not an insult aimed at the user.

### coder — programmer humour
- Jargon kept in English where Spanish devs actually use it: "Hola, mundo",
  "404: internet no encontrado", "build en verde", "tests en verde",
  "Recolectando basura" (garbage collecting), "Async en curso",
  "Bisect apunta ahí", "Lint de seguridad: aprobado", "Handshake TLS limpio".
- "Ship it." → "A producción." (the idiomatic Spanish-dev equivalent).
- "Cache miss city." → "Puro fallo de caché." ("city" as a suffix has no
  Spanish equivalent; kept the meaning and the dry tone.)
- "grep for it: ..." kept verbatim, including the English search terms
  ("slow", "dns", "wifi"), since those are literal search strings.
- "Timeout exceeded." → "Tiempo de espera excedido." (deliberately formal,
  matching the coder's terse style).

### genz — current pan-Hispanic youth slang (dates fast — see warning)
- **Warning: this file dates fastest.** Slang chosen is what is widely used
  across Latin America and Spain in 2026, but several items ("ghostear",
  "W"/"L", "vibras", "protagonista", "modo speedrun") may feel stale within
  a year or two. Deliberately avoided region-only slang: no "güey", "che",
  "vos", "tío", "chido", "guay", "mola", "qué pasada", "la está liando".
- "ghosted you" → "te ghosteó" (Spanglish verb, now pan-Hispanic in youth
  speech); "left you on read" → "te dejó en visto" (universal).
- "Vibes are immaculate" → "Las vibras están impecables"; "vibe check" →
  "prueba de vibras"; "Quiet era" → "Era de calma".
- "Rare W" → "Una W rara"; "Big L for them" → "Una L para ellos";
  "+{v} points. W." → "+{v} puntos. Una W." (W/L kept as letters — they are
  gaming-slang tokens in Spanish youth speech too.)
- "cooked" (PC) → "Tu PC está frita" (widely understood); "mid" → "mediocre";
  "sus" → "sospechoso"; "lowkey elite" → "discretamente élite"; "fr" →
  "en serio"; "the tea" → "el chisme" (pan-Hispanic for gossip).
- "Ate that, no crumbs." → "Lo rompiste, sin dejar nada." ("Lo rompiste" =
  "you killed it", the current pan-Hispanic equivalent; "no crumbs" has no
  Spanish equivalent and was dropped rather than forced.)
- "Nah that's not it, bestie." → "No, eso no es, amigo." ("bestie" has no
  natural Spanish equivalent; "amigo" keeps it warm without inventing slang.)
- "It's giving speedrun." → "Se acabó el tiempo, modo speedrun."
- "Home network ate." → "La red de casa va top." ("va top" is current and
  widely used; "top" as an adjective is pan-Hispanic youth speech.)

### dark-ages — medieval voice (silly, never grim)
- Archaic flavour carried by vocabulary, not by grammar: "¡Escuchad!",
  "pregón/pregonero" (town crier), "pergamino", "escriba", "bufón",
  "heraldo", "por mi fe", "buen señor", "¡Al torneo!", "por palabra del rey".
  The user is addressed as "tú" (modern 2nd person) throughout — no
  "vosotros" forms are used anywhere in the file.
- **One flagged choice:** "app.open|¡Escuchad! La guardia de la red
  comienza, {addr}." uses the vosotros imperative "escuchad" (from English
  "Hark!"). This is a deliberate archaic-theatrical form, not modern
  peninsular usage, and it is the only vosotros-form in the file. If the
  no-vosotros rule is meant to be absolute even for archaic flavour, swap to
  "¡Escucha!" or "¡Atención!". I kept "¡Escuchad!" because "Hark!" needs a
  plural/archaic call and "¡Escucha!" reads as modern, not medieval.
- "Thy" → "tu" (not "vuestro", which would read as Shakespearean/peninsular
  formal and clash with the silly-medieval register).
- "The peasants report no problems." → "Los aldeanos no reportan problemas."
  ("reportan" is Latin-American usage; peninsular would be "informan" — the
  Latin-American form was chosen as the more pan-Hispanic one here.)
- "Witchcraft? No, good fibre." → "¿Brujería? No, buena fibra."
- "Bandits, surely." → "Bandidos, seguro." (kept the joke; "bandidos" is not
  a banned word — no violence is described, just blamed.)
- "Thou art knighted" → "Quedas armado caballero".
- "The candle has burned down." → "La vela se consumió."

### industrial — factory / machine voice
- Steam-age factory floor throughout: "Calderas encendidas", "Girando los
  engranajes", "Generando vapor", "A todo vapor", "medidores", "tuberías",
  "válvula", "correa", "telares", "capataz", "turno", "silbato".
- "The machine runs." → "La máquina funciona."; "Pressure backs up" →
  "La presión sube"; "The works hum along nicely" → "La fábrica zumba
  muy bien."
- "Threw a spanner in" → "metió un palo en la rueda" (the Spanish idiom for
  sabotaging machinery — a closer fit than a literal spanner).
- "The exchange floor is running." (market.open) → "La bolsa está en marcha."
- "Safety inspection" → "Inspección de seguridad"; "safety faults" →
  "fallas de seguridad" (Latin-American "falla"; peninsular "fallo" — "falla"
  chosen as more widely understood across the Americas).

### mission — mission control, calm NASA-style procedure
- Callsign "flight" → "vuelo" throughout ("Vuelo, tenemos luz verde...",
  "Un comando a la vez, vuelo", "Buen trabajo, vuelo").
- "All systems go" / "go" / "no-go" → "luz verde" / "sin luz verde"
  (Spanish mission-control equivalent; "go" left untranslated would read
  as English in the UI).
- "Telemetry nominal" → "Telemetría nominal"; "Downrange" → "lejos" /
  "Lejos." (kept short; "río abajo" would be a mistranslation of the
  range term).
- "Loss of signal" → "Pérdida de señal" (standard Spanish space-ops term).
- "Standby" → "En espera."; "Stand by" → "En espera."
- "Negative. Re-check and retry." → "Negativo. Revisa y reintenta."
- "Clock's at zero." → "El reloj está en cero."
- "Payload" → "Carga" ("Carga cifrada", "Carga abierta").

## Lines I was unsure about

1. **genz — quiz.right** — EN: "Ate that, no crumbs." → ES: "Lo rompiste,
   sin dejar nada." The "no crumbs" half has no Spanish equivalent; the
   translation keeps the praise ("you smashed it") and drops the crumb
   wordplay. Alternative: "Lo hiciste perfecto, sin dejar nada." — slightly
   less slangy. Current choice is fine, but a native Gen-Z reviewer should
   confirm "Lo rompiste" still lands.
2. **genz — speedrunner** — EN: "Speedrun any%? Still on the last one." →
   ES: "¿Speedrun? Aún voy por la anterior." The "any%" joke (a speedrun
   category) was dropped: "¿Speedrun any%?" would be opaque to non-gamers
   in Spanish and the line is already slang-dense. "Speedrun" itself is kept
   as a loanword (widely used in Spanish gaming speech). If the "any%" gag
   matters, "¿Speedrun any%? Aún voy por la anterior." also fits the limits
   (7 words, 42 chars).
3. **dark-ages — app.open ("Hark!")** — see the "¡Escuchad!" note above. It
   is the only vosotros-form in the whole delivery. Swap to "¡Escucha!" or
   "¡Atención!" if the rule is absolute.
4. **emo — speedrunner** — EN: "Patience is a vibe." → ES: "La paciencia
   es un estado." "Vibe" is untranslatable here without sounding forced
   ("una vibra" would be wrong in emo register). "Un estado" (a state of
   mind) keeps the resigned tone. Alternative: "La paciencia es todo un
   tema." — arguably closer to the shrug; current choice is acceptable.
5. **coder — dns.slow** — EN: "Cache miss city." → ES: "Puro fallo de
   caché." The English is a joke suffix ("X city" = "lots of X"); no Spanish
   equivalent exists, so the joke was replaced with a flat equivalent. A
   Spanish dev would more likely say "Caché: todo fallos." — either works.
6. **industrial — sec.issue** — EN: "{v} safety faults flagged." → ES:
   "{v} fallas de seguridad marcadas." "Falla" (LatAm) vs "fallo" (Spain)
   was a coin toss; "falla" chosen as more widely understood. Same choice
   in coder's "Puro fallo de caché" — note the inconsistency is deliberate
   (coder uses the Spain-favoured "fallo" because Spanish devs write
   "fallo de caché"; industrial uses LatAm "falla"). Flagging in case a
   single convention is preferred.
7. **mission — game.good** — EN: "Link stable. You are go." → ES: "Enlace
   estable. Tienes luz verde." "You are go" is a callsign-style clearance;
   "Tienes luz verde" is the natural Spanish rendering, but it loses the
   "you are go" phrasing. Alternative: "Enlace estable. Luz verde para ti."
8. **genz — all.good** — EN: "No problems. Your setup is lowkey elite." →
   ES: "Sin problemas. Tu setup es discretamente élite." "Lowkey" has no
   good Spanish slang equivalent; "discretamente" is correct but flatter
   than the English. "En secreto" was considered and rejected (wrong
   meaning). Acceptable as-is.
9. **dark-ages — pc.busy** — EN: "The PC labours like an ox." → ES: "La PC
   trabaja como un buey." "Buey" is correct and pan-Hispanic, but the image
   is slightly rustic rather than medieval-court. Alternative: "La PC tira
   como un buey de carga." — current choice is shorter and fine.
10. **emo — market.open** — EN: "The markets opened. The void opened too."
    → ES: "Los mercados abrieron. El vacío también." Kept the void joke
    because the English header says "never dark" and the line is already in
    the source; "vacío" is abstract/melodramatic, not a banned topic. If
    even this is too dark, "Los mercados abrieron. El resto, también." is a
    safe fallback.

## Verification performed

A Python check (replicating the `Lines.cs` self-test: word count, char
count, placeholder allowlist, banned-word regex, emoji/surrogate scan) plus
key-order/variant-count comparison against the English originals was run on
all six files: **0 problems, 553/553 content lines**. A separate grep sweep
confirmed no vosotros forms (other than the flagged dark-ages "¡Escuchad!"),
no profanity, and no region-only slang in the six files. (One grep hit on
"puta" inside mission's "ComPUTAdora" is a substring false positive — the
word is "computadora".)
